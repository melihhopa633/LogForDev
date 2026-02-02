# Contributing to LogForDev

First off, thanks for taking the time to contribute! 🎉

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues. When creating a bug report, include:

- **Clear title** describing the issue
- **Steps to reproduce** the behavior
- **Expected behavior** vs **actual behavior**
- **Environment details** (OS, .NET version, Docker version)
- **Logs or screenshots** if applicable

### Suggesting Features

Feature suggestions are welcome! Please:

- Check if the feature has already been suggested
- Provide a clear description of the feature
- Explain why this feature would be useful
- Provide examples of how it would work

### Pull Requests

1. Fork the repo
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

#### Pull Request Guidelines

- Follow the existing code style
- Update documentation if needed
- Add tests for new features
- Ensure all tests pass
- Keep PRs focused - one feature/fix per PR

## Development Setup

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/LogForDev.git
cd LogForDev

# Start ClickHouse
docker-compose up -d clickhouse

# Run the app
cd src/LogForDev
dotnet run

# Run tests
dotnet test
```

## Code Style

- Use meaningful variable and method names
- Keep methods small and focused
- Add XML comments for public APIs
- Follow C# naming conventions

## Commit Messages

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit first line to 72 characters
- Reference issues when applicable

Examples:
```
Add batch log endpoint
Fix memory leak in WebSocket handler
Update README with Docker instructions
```

## Questions?

Feel free to open an issue with your question or reach out to the maintainers.

Thank you for contributing! 🙏
