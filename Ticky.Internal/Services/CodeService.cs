using Microsoft.EntityFrameworkCore;

namespace Ticky.Internal.Services;

public class CodeService
{
    private readonly DataContext _dataContext;

    public CodeService(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Code> CreateCodeAsync(User account, CodePurpose codePurpose)
    {
        var code = new Code
        {
            UserId = account.Id,
            CodePurpose = codePurpose,
            Value = await GetUnusedCodeAsync(),
            CreatedAt = DateTime.Now
        };

        account.EmailVerificationCode = code;
        _dataContext.Update(account);
        await _dataContext.SaveChangesAsync();

        return code;
    }

    private async Task<string> GetUnusedCodeAsync()
    {
        const string alphabet = "123456789";
        var random = new Random();

        do
        {
            string code = string.Empty;

            for (int i = 0; i < 3; i++)
            {
                code += alphabet[random.Next(alphabet.Length - 1)];
            }

            code += "-";

            for (int i = 0; i < 3; i++)
            {
                code += alphabet[random.Next(alphabet.Length - 1)];
            }

            var match = await _dataContext.Codes.AnyAsync(x => x.Value.Equals(code));

            if (!match)
                return code;
        } while (true);
    }
}
