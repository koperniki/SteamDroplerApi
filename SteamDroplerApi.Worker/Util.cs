using SteamKit2;

namespace SteamDroplerApi.Worker
{
	public static class Util
	{
		private const byte TimeoutForLongRunningTasksInSeconds = 60;
		public static long GetSystemUnixTime()
		{
			return (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
		}

		public static byte[] HexStringToByteArray(string hex)
		{
			int hexLen = hex.Length;
			byte[] ret = new byte[hexLen / 2];
			for (int i = 0; i < hexLen; i += 2)
			{
				ret[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			}
			return ret;
		}
		
		public static Task<T> ToLongRunningTask<T>(this AsyncJob<T> job) where T : CallbackMsg {
			if (job == null) {
				throw new ArgumentNullException(nameof(job));
			}

			job.Timeout = TimeSpan.FromSeconds(TimeoutForLongRunningTasksInSeconds);

			return job.ToTask();
		}
		
	}

	public static class APIEndpoints
	{
		public const string STEAMAPI_BASE = "https://api.steampowered.com";
		public const string COMMUNITY_BASE = "https://steamcommunity.com";
		public const string MOBILEAUTH_BASE = STEAMAPI_BASE + "/IMobileAuthService/%s/v0001";
		public static string MOBILEAUTH_GETWGTOKEN = MOBILEAUTH_BASE.Replace("%s", "GetWGToken");
		public const string TWO_FACTOR_BASE = STEAMAPI_BASE + "/ITwoFactorService/%s/v0001";
		public static string TWO_FACTOR_TIME_QUERY = TWO_FACTOR_BASE.Replace("%s", "QueryTime");
	}
}
