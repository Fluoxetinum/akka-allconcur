using System;
using System.Runtime.Serialization.Formatters;

namespace AkkaAllConcur.Messages
{
    class Abroadcast
    {
        public object Value { get; private set; }
        public Abroadcast(object v)
        {
            Value = v;
        }
        public override string ToString()
        {
            if (Value == null) return "¤";
            return Value.ToString();
        }
    }
}
