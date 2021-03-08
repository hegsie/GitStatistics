using System.Collections.Generic;

namespace GitStatistics
{
    public class DictionaryWithDefault<TKey, TValue> : Dictionary<TKey, TValue>
    {
        TValue _default;
        public TValue DefaultValue
        {
            get => _default;
            set => _default = value;
        }
        public DictionaryWithDefault() : base() { }
        public DictionaryWithDefault(TValue defaultValue) : base()
        {
            _default = defaultValue;
        }
        public new TValue this[TKey key]
        {
            get => base.TryGetValue(key, out var t) ? t : _default;
            set => base[key] = value;
        }
    }
}
