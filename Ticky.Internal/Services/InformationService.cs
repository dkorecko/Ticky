using System.Text.Json;

namespace Ticky.Internal.Services;

public class InformationService
{
    public List<InformationDTO> Information { get; init; }

    public InformationService()
    {
        if (!File.Exists(Constants.INFORMATION_PATH))
            throw new FileNotFoundException(
                "Information file not found",
                Constants.INFORMATION_PATH
            );

        var information = JsonSerializer.Deserialize<List<InformationDTO>>(
            File.ReadAllText(Constants.INFORMATION_PATH)
        );

        if (information is null)
            throw new FormatException("Failed to parse information file");

        Information = information;
    }
}
