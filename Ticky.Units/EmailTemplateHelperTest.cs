using System.Text.RegularExpressions;
using Devity.Extensions.Templates;

namespace Ticky.Units;

public class EmailTemplateHelperTest
{
    private const string LINK = "<a href=\"https://evil.example/login\">Sign in again</a>";
    private const string IMAGE = "<img src=\"https://evil.example/pixel.png\" onerror=\"alert(1)\">";
    private const string SCRIPT = "<script>alert(1)</script>";

    private static Card NewCard(
        string name,
        string description,
        string[] subtasks,
        DateTime? deadline = null
    )
    {
        var board = new Board
        {
            Id = 3,
            Name = "Board",
            Description = string.Empty,
            Code = "TCK",
            ProjectId = 1,
        };
        var column = new Column
        {
            Id = 2,
            Name = "Column",
            BoardId = board.Id,
            Index = 0,
            Board = board,
        };

        var card = new Card
        {
            Id = 7,
            Name = name,
            Description = description,
            Number = 12,
            Index = 0,
            ColumnId = column.Id,
            Column = column,
            CreatedById = 1,
            Deadline = deadline,
        };
        card.Subtasks = subtasks
            .Select(
                (text, index) =>
                    new Subtask
                    {
                        Index = index,
                        Text = text,
                        Completed = index % 2 == 0,
                        CardId = card.Id,
                    }
            )
            .ToList();

        return card;
    }

    private static string RenderReminder(Card card) =>
        TemplateHelper.PopulateTemplate(
            EmailTemplateHelper.BuildReminderTemplate(
                new Reminder
                {
                    At = new DateTime(2026, 11, 15, 9, 30, 0),
                    CardId = card.Id,
                    Card = card,
                }
            )
        );

    private static string RenderDeadlineReminder(Card card) =>
        TemplateHelper.PopulateTemplate(EmailTemplateHelper.BuildDeadlineReminderTemplate(card));

    /// <summary>The names of all tags opened in the HTML, in order.</summary>
    private static string[] Tags(string html) =>
        Regex
            .Matches(html, "<([a-zA-Z][a-zA-Z0-9]*)")
            .Select(x => x.Groups[1].Value.ToLowerInvariant())
            .ToArray();

    private static readonly DateTime DEADLINE = new(2026, 11, 20, 12, 0, 0);

    private static readonly (string Name, Func<Card, string> Render)[] TEMPLATES =
    [
        ("reminder", RenderReminder),
        ("deadline reminder", RenderDeadlineReminder),
    ];

    private static Card Hostile() =>
        NewCard(
            $"Fix {LINK} {SCRIPT}",
            $"Look at {LINK}\n{IMAGE} {SCRIPT}",
            [$"{LINK}", IMAGE, SCRIPT],
            DEADLINE
        );

    private static Card Benign() =>
        NewCard("Fix the thing", "Look at it\nplease", ["one", "two", "three"], DEADLINE);

    [Test]
    public void Reminders_MarkupInCardText_IsShownAsText()
    {
        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(Hostile());

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Not.Contain("evil.example/login\">"), name);
                Assert.That(body, Does.Not.Contain("<img src=\"https://evil"), name);
                Assert.That(body, Does.Not.Contain("<script>"), name);
                Assert.That(body, Does.Not.Contain("Sign in again</a>"), name);
                Assert.That(
                    body,
                    Does.Contain("&lt;a href=&quot;https://evil.example/login&quot;&gt;Sign in again&lt;/a&gt;"),
                    name
                );
                Assert.That(body, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"), name);
            });
        }
    }

    [Test]
    public void Reminders_MarkupInCardText_AddsNoTags()
    {
        foreach (var (name, render) in TEMPLATES)
        {
            var benign = Tags(render(Benign()));
            var hostile = Tags(render(Hostile()));

            // The description has the same number of line breaks in both cards, so the markup is identical, tag
            // for tag; no tag, attribute or link came from the user.
            Assert.That(hostile, Is.EqualTo(benign), name);
            Assert.That(hostile.Count(x => x == "a"), Is.EqualTo(1), $"{name}: only the button links");
            Assert.That(hostile, Does.Not.Contain("script"), name);
        }
    }

    [Test]
    public void Reminders_QuotesInCardText_CannotLeaveTheirElement()
    {
        var card = NewCard("\" onmouseover=\"alert(1)", "'><b>x</b>", ["\"><i>y</i>"], DEADLINE);

        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(card);

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Not.Contain("<b>x</b>"), name);
                Assert.That(body, Does.Not.Contain("<i>y</i>"), name);
                Assert.That(body, Does.Contain("&quot; onmouseover=&quot;alert(1)"), name);
            });
        }
    }

    [Test]
    public void Reminders_LineBreaksInDescription_BecomeBreaks()
    {
        var card = NewCard(
            "Card",
            "first\r\nsecond\nthird\rfourth <b>bold</b>",
            [],
            DEADLINE
        );

        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(card);

            Assert.That(
                body,
                Does.Contain("first<br />second<br />third<br />fourth &lt;b&gt;bold&lt;/b&gt;"),
                name
            );
        }
    }

    [Test]
    public void Reminders_PlainText_IsKept()
    {
        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(Benign());

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Contain("Fix the thing"), name);
                Assert.That(body, Does.Contain("Look at it<br />please"), name);
                Assert.That(body, Does.Contain("TCK-12"), name);
                Assert.That(body, Does.Contain("one").And.Contain("two").And.Contain("three"), name);
                Assert.That(body, Does.Contain("href=\"/boards/3/7\""), name);
            });
        }
    }

    /// <summary>
    /// Devity's <c>AddKey</c> encodes values itself, so text must be encoded exactly once: "R&amp;D" in the HTML, which
    /// reads "R&amp;D" in the mail, never "R&amp;amp;D", which would show the entity.
    /// </summary>
    [Test]
    public void Reminders_SpecialCharacters_AreEncodedOnce()
    {
        var card = NewCard("R&D \"launch\"", "Q&A\n5 < 6", ["Tom & Jerry"], DEADLINE);

        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(card);

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Contain("R&amp;D &quot;launch&quot;"), name);
                Assert.That(body, Does.Contain("Q&amp;A<br />5 &lt; 6"), name);
                Assert.That(body, Does.Contain("Tom &amp; Jerry"), name);
                Assert.That(body, Does.Not.Contain("&amp;amp;").And.Not.Contain("&amp;quot;").And.Not.Contain("&amp;lt;"), name);
                Assert.That(body, Does.Not.Contain("&lt;br /&gt;"), name);
            });
        }
    }

    [Test]
    public void Reminders_NoDescriptionNoSubtasks_LeavesTheSectionsOut()
    {
        var card = NewCard("Card", string.Empty, [], DEADLINE);

        foreach (var (name, render) in TEMPLATES)
        {
            var body = render(card);

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Not.Contain("📝"), $"{name}: description section");
                Assert.That(body, Does.Not.Contain("📋"), $"{name}: subtasks section");
                Assert.That(body, Does.Not.Contain("{CARD_").And.Not.Contain("{SUBTASK_"), name);
            });
        }
    }

    [Test]
    public void CardUrl_IsEncodedForTheAttribute()
    {
        var previous = Ticky.Base.Constants.BASE_URL;
        Ticky.Base.Constants.BASE_URL = "https://ticky.example/?a=1&b=\"2\"";

        try
        {
            foreach (var (name, render) in TEMPLATES)
            {
                var body = render(Benign());

                Assert.That(
                    body,
                    Does.Contain("href=\"https://ticky.example/?a=1&amp;b=&quot;2&quot;/boards/3/7\""),
                    name
                );
            }
        }
        finally
        {
            Ticky.Base.Constants.BASE_URL = previous;
        }
    }

    [Test]
    public void Encode_HandlesNull() => Assert.That(EmailTemplateHelper.Encode(null), Is.Empty);

    [TestCase("plain", "plain")]
    [TestCase("a\r\nBcc: someone@example.com", "a Bcc: someone@example.com")]
    [TestCase("a\nb\rc", "a b c")]
    [TestCase("  padded \n", "padded")]
    public void ToSubject_HasNoLineBreaks(string value, string expected)
    {
        var subject = EmailTemplateHelper.ToSubject(value);

        Assert.Multiple(() =>
        {
            Assert.That(subject, Is.EqualTo(expected));
            Assert.That(subject, Does.Not.Contain('\r').And.Not.Contain('\n'));
        });
    }
}
