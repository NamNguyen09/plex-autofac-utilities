using static Plex.Extensions.DbContext.Constants;

namespace Plex.Extensions.DbContext;
public class PlexDbOptions
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<PlexDbOptions> _logger;
	private readonly IHttpContextAccessor _contextAccessor;

	private static int? _commandTimeOut;
	private static int? _maxRetryCount;
	private static bool? _enableMigration;
	private static bool? _useLazyLoading;
	private static bool? _useChangeTrackingProxies;
	private static bool? _useQueryTrackingBehavior;
	private static Dictionary<string, string>? _dbProviderMappings;

	public PlexDbOptions(IConfiguration configuration,
						 ILogger<PlexDbOptions> logger,
						 IHttpContextAccessor contextAccessor)
	{
		_configuration = configuration;
		_logger = logger;
		_contextAccessor = contextAccessor;
		_commandTimeOut ??= Convert.ToInt32(_configuration.GetConfigValue(AppSettingKeys.CommandTimeOut, defaultValue: "300"));
		_maxRetryCount ??= Convert.ToInt32(_configuration.GetConfigValue(AppSettingKeys.SqlMaxRetryOnFailureCount, defaultValue: "0"));
		_enableMigration ??= Convert.ToBoolean(_configuration.GetConfigValue(AppSettingKeys.EnableMigration, defaultValue: "false"));
		_useLazyLoading ??= Convert.ToBoolean(_configuration.GetConfigValue(AppSettingKeys.UseLazyLoading, defaultValue: "false"));
		_useChangeTrackingProxies ??= Convert.ToBoolean(_configuration.GetConfigValue(AppSettingKeys.UseChangeTrackingProxies, defaultValue: "false"));
		_useQueryTrackingBehavior ??= Convert.ToBoolean(_configuration.GetConfigValue(AppSettingKeys.UseQueryTrackingBehavior, defaultValue: "false"));
		if (_dbProviderMappings == null)
		{
			_dbProviderMappings = [];
			_configuration.GetSection("AppSettings:DbProviderMappings").Bind(_dbProviderMappings);
		}
		(DbProvider, ConnectionString) = _configuration.GetDynamicConnectionString(_contextAccessor.HttpContext?.Request, _logger, DbProviderMappings);
	}
	public int CommandTimeOut => _commandTimeOut == null ? 300 : _commandTimeOut.Value;
	public int MaxRetryCount => _maxRetryCount == null ? 0 : _maxRetryCount.Value;
	public bool EnableMigration => _enableMigration != null && _enableMigration.Value;
	public bool UseLazyLoading => _useLazyLoading != null && _useLazyLoading.Value;
	public bool UseChangeTrackingProxies => _useChangeTrackingProxies != null && _useChangeTrackingProxies.Value;
	public bool UseQueryTrackingBehavior => _useQueryTrackingBehavior != null && _useQueryTrackingBehavior.Value;
	public string ConnectionString { get; set; } = "";
	public string DbProvider { get; set; } = MSSQL;
	public Dictionary<string, string>? DbProviderMappings => _dbProviderMappings?.ToDictionary();
}
