# Contributing to Ticky

Thank you for considering contributing to Ticky! This document outlines the process for contributing to the project.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the issue tracker to see if the problem has already been reported. If it has and the issue is still open, add a comment to the existing issue instead of opening a new one (or add a reaction).

When you are creating a bug report, please include as many details as possible:

- Use a clear and descriptive title
- Describe the exact steps to reproduce the problem
- Describe the behavior you observed and what you expected to see
- Include screenshots if possible
- Include details about your environment (OS, browser, etc.)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

- A clear and descriptive title
- A detailed description of the proposed functionality
- Any possible implementation details or ideas
- Why this enhancement would be useful to most users

### Pull Requests

Please follow these steps for submitting a pull request:

1. Fork the repository
2. Create a new branch for your feature (`git checkout -b feature/name-of-feature`)
3. Make your changes
4. Verify the changes
5. Commit your changes (`git commit -m 'Add some feature'`)
6. Push to your branch (`git push origin feature/name-of-feature`)
7. Open a Pull Request

#### Pull Request Guidelines

- Update the README.md with details of changes if applicable
- Follow the code style of the project
- Reference any relevant issues in your PR description

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- MySQL Server 8.0+
- IDE (Visual Studio, VS Code, etc.)

### Setup Steps

1. Clone your fork of the repository
2. Configure your database and e-mail connection in user secrets

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Production": "Server=localhost;Database=ticky;Uid=ticky;Pwd={PASSWORD_HERE};",
    "Development": "Server=localhost;Database=ticky;Uid=ticky;Pwd={PASSWORD_HERE};"
  },
  "Email": {
    "Server": "{SMTP_HOST}",
    "Port": 465,
    "SenderName": "Ticky",
    "SenderEmail": "{SMTP_EMAIL}",
    "Account": "{SMTP_USERNAME}",
    "Password": "{SMTP_PASSWORD}",
    "Security": true
  }
}
```

3. Start the application: `dotnet watch --project Ticky.Web/Ticky.Web.csproj`

## Coding Conventions

- Use the built-in code formatter
- Follow C# naming conventions
- Add comments for complex logic
- Write descriptive commit messages

## Documentation

Good documentation is essential. Please update relevant documentation when making changes:

- Update the README.md if relevant
- Consider adding to the wiki for significant features

## Questions?

If you have any questions about contributing, please open an issue with your question.

Thank you for contributing to Ticky!
