# MonsterASP.NET Deployment Mode

This project supports two runtime modes.

## Local development (full cloud/distributed demo)

`appsettings.Development.json` enables:

- LocalStack S3
- LocalStack SQS + DLQ
- LocalStack DynamoDB
- Background ingestion worker
- Redis cache
- Qdrant Cloud
- Gemini

Run Docker/LocalStack/Redis locally and use Terraform for the LocalStack resources.

## MonsterASP.NET / Production (zero AWS cost)

`appsettings.json` intentionally sets:

- `CloudInfrastructure:Enabled = false`
- `Redis:Enabled = false`

Uploads use a direct ingestion path:

PDF -> ASP.NET Core -> text extraction -> chunking -> Gemini embeddings -> Qdrant Cloud

The public application therefore does not require AWS, LocalStack, Redis, Docker, or a continuously-running SQS worker on the Monster host.

## Required Monster environment variables

Set these in the MonsterASP.NET website environment-variable/configuration area:

- `Gemini__ApiKey` = your Gemini API key
- `Qdrant__ApiKey` = your Qdrant Cloud API key

Optional if your Qdrant host changes:

- `Qdrant__Host`
- `Qdrant__UseHttps` = `true`

Do not put production API keys into `appsettings.json`.

## Local secrets

For Visual Studio/local development, use .NET User Secrets or environment variables rather than committing keys.

Example commands from the project folder:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_KEY"
dotnet user-secrets set "Qdrant:ApiKey" "YOUR_QDRANT_KEY"
```

If `UserSecretsId` is not present in the project file, `dotnet user-secrets init` adds it automatically.
