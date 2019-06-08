using System;
using System.Runtime.Serialization.Formatters;

namespace AkkaAllConcur.Messages
{
    class BroadcastAtomically
    {
        public object Value { get; private set; }
        public BroadcastAtomically(object v)
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
