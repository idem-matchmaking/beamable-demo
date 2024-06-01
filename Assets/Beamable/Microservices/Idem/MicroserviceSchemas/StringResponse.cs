namespace Beamable.Microservices.MicroserviceSchemas
{
    public class StringResponse : BaseResponse
    {
        public readonly string value;

        public StringResponse(string value) : base(true)
        {
            this.value = value;
        }
    }
}