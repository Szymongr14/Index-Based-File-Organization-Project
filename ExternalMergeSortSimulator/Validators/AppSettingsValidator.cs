using MemoryPageAccessSimulator.Models;

namespace ExternalMergeSortSimulator.Validators;

using FluentValidation;

public class AppSettingsValidator : AbstractValidator<AppSettings>
{
    public AppSettingsValidator()
    {
        RuleFor(x => x.PageSizeInNumberOfRecords)
            .NotEmpty().GreaterThan(0).WithMessage("PageSizeInNumberOfRecords must be greater than 0.");
        
        RuleFor(x => x.PageSizeInNumberOfRecords)
            .NotEmpty().GreaterThan(0).WithMessage("PageSizeInNumberOfRecords must be greater than 0.");

        RuleFor(x => x.DataSource)
            .NotEmpty().WithMessage("DataSource is required.")
            .Must(value => new[] { "GenerateRandomly", "ProvideManually", "LoadFromFile" }.Contains(value))
            .WithMessage("DataSource must be one of: 'GenerateRandomly', 'ProvideManually', or 'LoadFromFile'.");

        RuleFor(x => x.NumberOfRecordsToGenerate)
            .NotEmpty().GreaterThan(0).When(x => x.DataSource == "GenerateRandomly")
            .WithMessage("NumberOfRecordsToGenerate must be specified and greater than 0 when DataSource is 'GenerateRandomly'.");

        RuleFor(x => x.FilePathToRecords)
            .NotEmpty().When(x => x.DataSource == "LoadFromFile")
            .WithMessage("FilePath is required when DataSource is 'LoadFromFile'.");
        
        RuleFor(x => x.FilePathToInstructions)
            .NotEmpty()
            .WithMessage("FilePath is required when DataSource is 'LoadFromFile'.");
        
        RuleFor(x => x.RecordSizeInBytes)
            .NotEmpty().GreaterThan(0).WithMessage("RecordSizeInBytes must be greater than 0.");
        
        RuleFor(x => x.LogLevel).NotEmpty().WithMessage("LogLevel is required.")
            .Must(value => new[] { "Detailed", "Basic"}.Contains(value)).WithMessage("LogLevel must be one of: 'Detailed', 'Basic'.");
    }
}