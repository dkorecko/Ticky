using Microsoft.AspNetCore.Identity;

namespace Ticky.Units;

public class IdentityResultHelperTest
{
    private static readonly IdentityErrorDescriber Describer = new();

    [Test]
    public void DuplicateUserName_IsADuplicateAccount() =>
        Assert.That(
            IdentityResult.Failed(Describer.DuplicateUserName("a@test.com")).IsDuplicateAccount(),
            Is.True
        );

    [Test]
    public void DuplicateEmail_IsADuplicateAccount() =>
        Assert.That(
            IdentityResult.Failed(Describer.DuplicateEmail("a@test.com")).IsDuplicateAccount(),
            Is.True
        );

    [Test]
    public void DuplicateAlongOtherErrors_IsADuplicateAccount() =>
        Assert.That(
            IdentityResult
                .Failed(Describer.DuplicateUserName("a@test.com"), Describer.InvalidEmail("a"))
                .IsDuplicateAccount(),
            Is.True
        );

    [Test]
    public void PasswordErrors_AreNotADuplicateAccount() =>
        Assert.That(
            IdentityResult
                .Failed(Describer.PasswordTooShort(6), Describer.PasswordRequiresDigit())
                .IsDuplicateAccount(),
            Is.False
        );

    [Test]
    public void InvalidEmail_IsNotADuplicateAccount() =>
        Assert.That(
            IdentityResult.Failed(Describer.InvalidEmail("a")).IsDuplicateAccount(),
            Is.False
        );

    [Test]
    public void Success_IsNotADuplicateAccount() =>
        Assert.That(IdentityResult.Success.IsDuplicateAccount(), Is.False);
}
