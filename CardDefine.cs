﻿using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
namespace TouhouCardEngine
{
    /// <summary>
    /// 卡片定义，包含了一张卡的静态数据和效果逻辑。
    /// </summary>
    [Serializable]
    public class CardDefine
    {
        #region 方法
        public CardDefine(int id, string type, Dictionary<string, object> props, Effect[] effects = null)
        {
            _id = id;
            _type = type;
            if (props != null)
                _propDict = props;
            if (effects != null)
                _effectList.AddRange(effects);
        }
        public CardDefine(int id, string type) : this(id, type, null, null)
        {
        }
        /// <summary>
        /// 供序列化使用的默认构造器
        /// </summary>
        public CardDefine() : this(0, string.Empty, null, null)
        {
        }
        public CardDefine(CardDefine other)
        {
            cardPoolId = other.cardPoolId;
            _id = other._id;
            _type = other._type;
            if (other._propDict != null)
            {
                foreach (var prop in other._propDict)
                {
                    _propDict.Add(prop.Key, prop.Value);
                }
            }
            if (other._effectList != null)
            {
                foreach (var effect in other._effectList)
                {
                    _effectList.Add(effect);
                }
            }
        }
        public override bool Equals(object obj)
        {
            if (obj is CardDefine other)
                return cardPoolId == other.cardPoolId && id == other.id;
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            // 同DefineReference.GetHashCode()。
            int hashCode = 17;
            hashCode = hashCode * 31 + cardPoolId.GetHashCode();
            hashCode = hashCode * 31 + id.GetHashCode();
            return hashCode;
        }
        public virtual void setProp<T>(string propName, T value)
        {
            if (propName == nameof(id))
                id = (int)(object)value;
            _propDict[propName] = value;
        }
        public virtual T getProp<T>(string propName)
        {
            if (_propDict.ContainsKey(propName) && _propDict[propName] is T t)
                return t;
            else
                return default;
        }
        public virtual bool removeProp(string propName)
        {
            return _propDict.Remove(propName);
        }
        public virtual string[] getPropNames()
        {
            return _propDict.Keys.ToArray();
        }
        public virtual bool hasProp(string propName)
        {
            return _propDict.ContainsKey(propName);
        }
        public object this[string propName]
        {
            get { return getProp<object>(propName); }
        }
        public virtual Effect[] getEffects()
        {
            return _effectList.ToArray();
        }
        public virtual void setEffects(Effect[] value)
        {
            _effectList.Clear();
            _effectList.AddRange(value);
        }
        public int getEffectIndex(Effect effect)
        {
            return _effectList.IndexOf(effect);
        }
        public GeneratedEffect[] getGeneratedEffects()
        {
            return _effectList.OfType<GeneratedEffect>().ToArray();
        }
        public DefineReference getDefineRef()
        {
            return new DefineReference(cardPoolId, id);
        }
        public override string ToString()
        {
            return $"{{{cardPoolId},{id}}}";
        }
        public string getFormatString()
        {
            return $"{{cardDefine:{cardPoolId},{id}}}";
        }
        #endregion
        #region 属性字段
        /// <summary>
        /// 卡片定义ID，这个ID应该是独特的并用于区分不同的卡片。
        /// </summary>
        [BsonIgnore]
        public virtual int id
        {
            get { return _id; }
            set { _id = value; }
        }
        [BsonElement]
        int _id;
        [BsonIgnore]
        public virtual string type
        {
            get { return _type; }
            set { _type = value; }
        }
        [NonSerialized]
        public long cardPoolId;
        [BsonElement]
        string _type;
        [BsonElement]
        Dictionary<string, object> _propDict = new Dictionary<string, object>();
        [BsonElement]
        List<Effect> _effectList = new List<Effect>();
        #endregion
    }
    /// <summary>
    /// 忽略这张卡
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class IgnoreCardDefineAttribute : Attribute
    {
    }
}