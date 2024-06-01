using System;

namespace Beamable.Microservices.Idem.Tools
{
    public class ScopedLambda : IDisposable
    {
        private readonly Action _onDispose;

        public ScopedLambda(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }

    public static class Extentions
    {
        public static string ToJson(this object obj, bool indentation = false)
            => CompactJson.Serializer.ToString(obj, indentation);
    }
}