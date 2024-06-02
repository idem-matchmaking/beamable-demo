namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
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