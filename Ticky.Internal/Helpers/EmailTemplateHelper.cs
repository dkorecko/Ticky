using System.Net;
using Devity.Extensions.Templates;

namespace Ticky.Internal.Helpers;

/// <summary>
/// Builds the reminder e-mail templates. <c>TemplateHelper.PopulateTemplate</c> inserts values into the HTML as they
/// are, so every value that comes from a user (card name, description, subtasks, ...) is HTML-encoded here; otherwise
/// a board member could put links and markup into the mails Ticky sends to the card's assignees and creator.
/// Building the template is separate from sending it, so the rendered body can be checked without SMTP.
/// </summary>
public static class EmailTemplateHelper
{
    private static readonly string REMINDER = Path.Combine(Constants.Emails.BASE_PATH, "Reminder.html");
    private static readonly string DEADLINE_REMINDER = Path.Combine(
        Constants.Emails.BASE_PATH,
        "DeadlineReminder.html"
    );

    /// <summary>
    /// Text that goes between tags or into an attribute value of a template: markup is shown as text.
    /// </summary>
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Like <see cref="Encode"/>, but line breaks in the text become <c>&lt;br /&gt;</c> (after encoding, so the only
    /// markup in the result is the ones added here).
    /// </summary>
    public static string EncodeMultiline(string? value) =>
        Encode(value).ReplaceLineEndings("<br />");

    /// <summary>
    /// Text for the subject of an e-mail. It's a header and not HTML, so it isn't encoded, but line breaks can't be
    /// part of it.
    /// </summary>
    public static string ToSubject(string? value) =>
        (value ?? string.Empty).ReplaceLineEndings(" ").Trim();

    public static DevityTemplate BuildReminderTemplate(Reminder reminder, string? templatePath = null)
    {
        var card = reminder.Card;
        var cardCode = GetCardCode(card);

        return new DevityTemplate(templatePath ?? REMINDER)
            .AddKey(Constants.Emails.Mappings.CARD_TEXT, Encode(card.Name))
            .AddKey(Constants.Emails.Mappings.CARD_SCHEDULED_FOR, reminder.At.ToReadableStringWithTime())
            .AddKey(Constants.Emails.Mappings.CARD_CODE, Encode(cardCode))
            .AddKey(Constants.Emails.Mappings.CARD_DESCRIPTION, EncodeMultiline(card.Description))
            .AddKey(Constants.Emails.Mappings.CARD_URL, Encode(GetCardUrl(card)))
            .AddKey(
                Constants.Emails.Mappings.CARD_DEADLINE,
                card.Deadline.GetValueOrDefault().ToReadableStringWithTime()
            )
            .AddCondition(Constants.Emails.Mappings.CARD_DEADLINE_SECTION, card.Deadline.HasValue)
            .AddCondition(
                Constants.Emails.Mappings.CARD_DESCRIPTION_SECTION,
                !string.IsNullOrWhiteSpace(card.Description)
            )
            .AddCondition(Constants.Emails.Mappings.CARD_SUBTASKS_SECTION, card.Subtasks.Any())
            .AddLoop(Constants.Emails.Mappings.CARD_SUBTASKS, BuildSubtaskLoop(card));
    }

    public static DevityTemplate BuildDeadlineReminderTemplate(Card card, string? templatePath = null)
    {
        var cardCode = GetCardCode(card);

        return new DevityTemplate(templatePath ?? DEADLINE_REMINDER)
            .AddKey(Constants.Emails.Mappings.CARD_TEXT, Encode(card.Name))
            .AddKey(
                Constants.Emails.Mappings.CARD_SCHEDULED_FOR,
                card.Deadline!.Value.ToReadableStringWithTime()
            )
            .AddKey(Constants.Emails.Mappings.CARD_CODE, Encode(cardCode))
            .AddKey(Constants.Emails.Mappings.CARD_DESCRIPTION, EncodeMultiline(card.Description))
            .AddKey(Constants.Emails.Mappings.CARD_URL, Encode(GetCardUrl(card)))
            .AddCondition(
                Constants.Emails.Mappings.CARD_DESCRIPTION_SECTION,
                !string.IsNullOrWhiteSpace(card.Description)
            )
            .AddCondition(Constants.Emails.Mappings.CARD_SUBTASKS_SECTION, card.Subtasks.Any())
            .AddLoop(Constants.Emails.Mappings.CARD_SUBTASKS, BuildSubtaskLoop(card));
    }

    public static string GetCardCode(Card card) => $"{card.Column.Board.Code}-{card.Number}";

    private static string GetCardUrl(Card card) =>
        $"{Constants.BASE_URL}/boards/{card.Column.BoardId}/{card.Id}";

    private static DevityTemplateLoop<Subtask> BuildSubtaskLoop(Card card) =>
        new DevityTemplateLoop<Subtask>(card.Subtasks)
            .AddKey(Constants.Emails.Mappings.SUBTASK_TEXT, s => Encode(s.Text))
            .AddKey(Constants.Emails.Mappings.SUBTASK_ICON, s => s.Completed ? "✓" : "○")
            .AddKey(Constants.Emails.Mappings.SUBTASK_ICON_COLOR, s => s.Completed ? "#4CAF50" : "#F44336")
            .AddKey(
                Constants.Emails.Mappings.SUBTASK_COMPLETED_CLASS,
                s => s.Completed ? " subtask-completed" : string.Empty
            );
}
