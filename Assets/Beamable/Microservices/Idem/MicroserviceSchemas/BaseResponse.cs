namespace Beamable.Microservices.MicroserviceSchemas
{
    public class BaseResponse
    {
        public static readonly BaseResponse Success = new(true);
        public static readonly BaseResponse UnauthorizedFailure = new(false, "Unauthorized access is not supported");
        public static readonly BaseResponse UnsupportedGameModeFailure = new(false, "Unsupported game mode");
        public static readonly BaseResponse IdemConnectionFailure = new(false, "No connection to Idem");
        public static readonly BaseResponse InternalErrorFailure = new(false, "Internal error");

        public readonly bool success;
        public readonly string error;

        public BaseResponse(bool success, string error = null)
        {
            this.success = success;
            this.error = error;
        }
    }
}