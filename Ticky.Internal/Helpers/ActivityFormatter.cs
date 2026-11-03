using System.Net;
using Ticky.Base.Converters;

namespace Ticky.Internal.Helpers;

/// <summary>
/// Builds the text shown for an activity from its data. This is the only place activity wording lives, so it is
/// where localization would plug in.
/// </summary>
public static class ActivityFormatter
{
    /// <summary>
    /// HTML describing what happened, without the person's name, e.g. "<b>added</b> label <b>Bug</b>".
    /// Every value is escaped.
    /// </summary>
    public static string ToHtml(this Activity activity)
    {
        if (activity.LegacyText is not null)
            return activity.LegacyText;

        return activity switch
        {
            CardMovedActivity x when x.FromColumnName is null => $"{B("moved")} the card to {V(x.ToColumnName)}",
            CardMovedActivity x => $"{B("moved")} the card from {V(x.FromColumnName)} to {V(x.ToColumnName)}",

            TitleChangedActivity { OldTitle: not null, NewTitle: not null } x =>
                $"{B("changed")} the title from {V(x.OldTitle)} to {V(x.NewTitle)}",
            TitleChangedActivity => $"{B("changed")} the title",
            DescriptionChangedActivity => $"{B("changed")} the description",
            CommentPostedActivity => $"{B("posted")} a comment",
            PriorityChangedActivity x => $"{B("changed")} the priority to {V(x.Priority?.ToString())}",
            DeadlineSetActivity x => $"{B("set")} the deadline for {V(Date(x.Deadline))}",
            DeadlineRemovedActivity => $"{B("deleted")} the deadline",
            SnoozedActivity x => $"{B("snoozed")} this card until {V(Date(x.Until))}",
            UnsnoozedActivity => $"{B("unsnoozed")} this card",
            RepeatSetActivity x => $"{B("set up card repeat")} for this card for {V(x.RepeatType?.GetDisplayName())}",
            RepeatDisabledActivity => $"{B("turned repeating off")} for this card",

            AssigneeAddedActivity { IsSelf: true } => $"{B("joined")} the card",
            AssigneeAddedActivity x => $"{B("added")} the user {V(x.AssigneeName)} as an assignee",
            AssigneeRemovedActivity { IsSelf: true } => $"{B("left")} the card",
            AssigneeRemovedActivity x => $"{B("removed")} the user {V(x.AssigneeName)} from the assignees",

            SubtaskAssigneeAddedActivity { SubtaskText: null } x =>
                $"{B("added")} the user {V(x.AssigneeName)} as a subtask assignee",
            SubtaskAssigneeAddedActivity x =>
                $"{B("added")} the user {V(x.AssigneeName)} as an assignee of subtask {V(x.SubtaskText)}",
            SubtaskAssigneeRemovedActivity { SubtaskText: null } x =>
                $"{B("removed")} the user {V(x.AssigneeName)} from the subtask assignees",
            SubtaskAssigneeRemovedActivity x =>
                $"{B("removed")} the user {V(x.AssigneeName)} from the assignees of subtask {V(x.SubtaskText)}",

            LabelAddedActivity x => $"{B("added")} label {V(x.LabelName)}",
            LabelRemovedActivity x => $"{B("removed")} label {V(x.LabelName)}",

            ReminderAddedActivity x => $"{B("added")} a reminder for {V(Date(x.At))}",
            ReminderRemovedActivity x => $"{B("deleted")} the reminder at {V(Date(x.At))}",

            SubtaskAddedActivity x => $"{B("added")} subtask {V(x.SubtaskText)}",
            SubtaskEditedActivity x => $"{B("edited")} subtask {V(x.OldSubtaskText)} to {V(x.SubtaskText)}",
            SubtaskCompletionChangedActivity x =>
                $"{B("marked")} the subtask {V(x.SubtaskText)} as {V(x.Completed == true ? "completed" : "to be done")}",
            SubtaskDeletedActivity x => $"{B("deleted")} subtask {V(x.SubtaskText)}",

            AttachmentUploadedActivity x => $"{B("uploaded")} file named {V(x.FileName)}",
            AttachmentDeletedActivity x => $"{B("deleted")} file named {V(x.FileName)}",

            TimeRecordAddedActivity x => $"{B("added")} a time record for {V(x.Duration?.ToElapsedString())}",
            TimeRecordChangedActivity x =>
                $"{B("changed")} the time record from {V(x.OldDuration?.ConvertToString())} to {V(x.Duration?.ConvertToString())}",
            TimeRecordDeletedActivity x => $"{B("deleted")} time record of {V(x.Duration?.ToElapsedString())}",

            LinkAddedActivity x => $"{B("added")} a linked issue {V(x.RelatedCardCode)} with {V(x.Category)} relationship",
            LinkRemovedActivity x => $"{B("deleted")} a linked issue {V(x.RelatedCardCode)} with {V(x.Category)} relationship",
            RepeatedFromActivity x => $"{B("was repeated")} from card {V(x.RelatedCardCode)}",
            RepeatCardCreatedActivity x => $"{B("created")} repeat card {V(x.RelatedCardCode)}",

            _ => string.Empty,
        };
    }

    /// <summary>The action word, emphasized.</summary>
    private static string B(string verb) => $"<b>{verb}</b>";

    /// <summary>A value, escaped and emphasized.</summary>
    private static string V(string? value) => $"<b>{WebUtility.HtmlEncode(value ?? "?")}</b>";

    private static string? Date(DateTime? value) => value?.ToReadableStringWithTime();
}
