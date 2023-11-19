﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TouhouCardEngine.Histories;
using TouhouCardEngine.Interfaces;
namespace TouhouCardEngine
{
    [Serializable]
    public abstract class Buff : IBuff, IChangeableBuff
    {
        #region 公有方法
        public Buff()
        {
        }
        #region 属性
        public object getProp(string propName)
        {
            if (propDict.TryGetValue(propName, out object value))
            {
                return value;
            }
            return null;
        }
        public T getProp<T>(string propName)
        {
            if (propDict.TryGetValue(propName, out object value) && value is T t)
                return t;
            else
                return default;
        }
        public Task<PropertyChangeEventArg> setProp(CardEngine game, string propName, object value)
        {
            if (game != null && game.triggers != null)
            {
                return game.triggers.doEvent(new PropertyChangeEventArg(this, propName, value, getProp(propName)), arg =>
                {
                    var argBuff = arg.buff;
                    var argPropName = arg.propName;
                    var argValue = arg.value;
                    var beforeValue = argBuff.getProp(argPropName);
                    argBuff.setPropRaw(argPropName, argValue);
                    addChange(new BuffPropChange(argBuff, argPropName, beforeValue, argValue));
                    //当Buff属性发生改变的时候，如果有属性修正器的属性和Buff关联，则改变它的值
                    updateModifierProps(game);
                    game.logger?.logTrace("Game", $"{argBuff}的属性{argPropName}=>{StringHelper.propToString(argValue)}");
                    return Task.CompletedTask;
                });
            }
            else
            {
                var beforeValue = getProp(propName);
                setPropRaw(propName, value);
                addChange(new BuffPropChange(this, propName, beforeValue, value));
                return Task.FromResult<PropertyChangeEventArg>(default);
            }
        }
        /// <summary>
        /// 更新所有与BUFF属性关联的修正器的值。
        /// </summary>
        public void updateModifierProps(CardEngine game)
        {
            //当Buff属性发生改变的时候，如果有属性修正器的属性和Buff关联，则改变它的值
            foreach (PropModifier propModifier in getPropertyModifiers(game))
            {
                if (propModifier.relatedPropName != null)
                {
                    if (propDict.TryGetValue(propModifier.relatedPropName, out object value))
                    {
                        propModifier.setValue(game, card, value);
                    }
                    else
                    {
                        propModifier.setValue(game, card, 0);
                    }
                }
            }
        }
        #endregion
        public void setInfo(IGame game, Card card, int id)
        {
            var beforeCard = this.card;
            var beforeInstanceId = instanceID;
            setInfoRaw(card, id);
            addChange(new BuffInfoChange(this, beforeCard, card, beforeInstanceId, id));
        }
        public abstract PropModifier[] getPropertyModifiers(CardEngine game);
        public virtual BuffExistLimit[] getExistLimits(CardEngine game)
        {
            return null;
        }
        public abstract IEffect[] getEffects(CardEngine game);
        public abstract Buff clone();
        #endregion

        #region 私有方法

        #region 接口实现
        void IChangeableBuff.setInfo(Card card, int id) => setInfoRaw(card, id);
        void IChangeableBuff.setProp(string propName, object value) => setPropRaw(propName, value);
        #endregion

        private void addChange(BuffChange change)
        {
            _changes.Add(change);
        }
        private void setInfoRaw(Card card, int id)
        {
            this.card = card;
            instanceID = id;
        }
        private void setPropRaw(string propName, object value)
        {
            propDict[propName] = value;
        }
        #endregion

        #region 属性字段
        [Obsolete]
        public abstract int id { get; }
        public int instanceID { get; private set; }
        public Card card { get; private set; }
        public Dictionary<string, object> propDict = new Dictionary<string, object>();
        private List<BuffChange> _changes = new List<BuffChange>();
        #endregion

        #region 嵌套类型
        [EventChildren(typeof(Card.PropChangeEventArg))]
        public class PropertyChangeEventArg : EventArg, ICardEventArg
        {
            public PropertyChangeEventArg(Buff buff, string propName, object value, object valueBeforeChanged)
            {
                setVar(VAR_BUFF, buff);
                setVar(VAR_PROPERTY_NAME, propName);
                setVar(VAR_VALUE, value);
                setVar(VAR_VALUE_BEFORE_CHANGED, valueBeforeChanged);
            }
            public override void Record(IGame game, EventRecord record)
            {
                record.setVar(VAR_BUFF, buff);
                record.setVar(VAR_PROPERTY_NAME, propName);
                record.setVar(VAR_VALUE, value);
                record.setVar(VAR_VALUE_BEFORE_CHANGED, valueBeforeChanged);
            }
            ICard ICardEventArg.getCard() => buff?.card;
            public Buff buff
            {
                get { return getVar<Buff>(VAR_BUFF); }
                set { setVar(VAR_BUFF, value); }
            }
            public string propName
            {
                get { return getVar<string>(VAR_PROPERTY_NAME); }
                set { setVar(VAR_PROPERTY_NAME, value); }
            }
            public object value
            {
                get { return getVar(VAR_VALUE); }
                set { setVar(VAR_VALUE, value); }
            }
            public object valueBeforeChanged
            {
                get { return getVar(VAR_VALUE_BEFORE_CHANGED); }
                set { setVar(VAR_VALUE_BEFORE_CHANGED, value); }
            }
            public const string VAR_BUFF = "Buff";
            public const string VAR_PROPERTY_NAME = "PropertyName";
            public const string VAR_VALUE = "Value";
            public const string VAR_VALUE_BEFORE_CHANGED = "ValueBeforeChanged";
        }
        #endregion
    }
}