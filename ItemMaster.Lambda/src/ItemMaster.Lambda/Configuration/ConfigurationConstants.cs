namespace ItemMaster.Lambda.Configuration;

public static class ConfigurationConstants
{
    // Environment Variables
    public const string DOTNET_ENVIRONMENT = "DOTNET_ENVIRONMENT";
    public const string CONFIG_BASE = "CONFIG_BASE";
    public const string REGION = "REGION";
    public const string ITEMMASTER_TEST_MODE = "ITEMMASTER_TEST_MODE";
    public const string APPLY_MIGRATIONS = "APPLY_MIGRATIONS";
    public const string APPLY_MIGATIONS = "APPLY_MIGATIONS";

    // Configuration Keys
    public const string LOG_LEVEL = "log_level";
    public const string SQS_URL = "sqs:url";
    public const string SQS_MAX_RETRIES = "sqs:max_retries";
    public const string SQS_BASE_DELAY_MS = "sqs:base_delay_ms";
    public const string SQS_BACKOFF_MULTIPLIER = "sqs:backoff_multiplier";
    public const string SQS_BATCH_SIZE = "sqs:batch_size";
    public const string SQS_CIRCUIT_BREAKER_FAILURE_THRESHOLD = "sqs:circuit_breaker_failure_threshold";
    public const string SQS_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS = "sqs:circuit_breaker_duration_of_break_seconds";
    public const string SQS_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS = "sqs:circuit_breaker_sampling_duration_seconds";
    public const string SQS_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT = "sqs:circuit_breaker_minimum_throughput";

    // Snowflake Configuration Keys
    public const string SNOWFLAKE_DATABASE = "snowflake:database";
    public const string SNOWFLAKE_SCHEMA = "snowflake:schema";
    public const string SNOWFLAKE_TABLE = "snowflake:table";
    public const string SNOWFLAKE_WAREHOUSE = "snowflake:warehouse";

    // Default Values
    public const string DEFAULT_LOG_LEVEL = "Information";
    public const int DEFAULT_MAX_RETRIES = 2;
    public const int DEFAULT_BASE_DELAY_MS = 1000;
    public const double DEFAULT_BACKOFF_MULTIPLIER = 2.0;
    public const int DEFAULT_BATCH_SIZE = 100;
    public const int DEFAULT_CIRCUIT_BREAKER_FAILURE_THRESHOLD = 5;
    public const int DEFAULT_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS = 30;
    public const int DEFAULT_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS = 60;
    public const int DEFAULT_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT = 3;

    // Cache Configuration
    public const int SECRET_CACHE_DURATION_MINUTES = 15;
    public const string IN_MEMORY_DB_NAME = "ItemMasterTest";
}