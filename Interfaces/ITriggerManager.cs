﻿using System;
using System.Threading.Tasks;
using TouhouCardEngine.Histories;

namespace TouhouCardEngine.Interfaces
{
    public interface ITriggerManager : IDisposable
    {
        void register(EventTriggerTime triggerTime, ITrigger trigger);
        void registerDelayed(EventTriggerTime triggerTime, ITrigger trigger);
        bool remove(EventTriggerTime triggerTime, ITrigger trigger);
        ITrigger[] getTriggers(EventTriggerTime triggerTime);
        Task<EventArg> doEvent(EventArg eventArg);
        event Action<IEventArg> onEventBefore;
        event Action<IEventArg> onEventAfter;
        IEventArg currentEvent { get; }
        IEventArg[] getEventChain();
        IEventArg[] getRecordedEvents(bool includeCanceled = false, bool includeUncompleted = false);
        [Obsolete]
        EventRecord[] getEventRecords(bool includeCanceled = false, bool includeUncompleted = false);
        [Obsolete]
        EventRecord getEventRecord(IEventArg eventArg, bool includeCanceled = false, bool includeUncompleted = false);
        int getCurrentEventIndex();
        int getEventIndexBefore(IEventArg eventArg);
        int getEventIndexAfter(IEventArg eventArg);
        void addChange(Change change);
        void revertChanges(IChangeable target, int eventIndex);
    }
    public interface ITrigger
    {
        bool checkCondition(IEventArg arg);
        Task invoke(IEventArg arg);
    }
    public interface IEventArg
    {
        bool isCanceled { get; set; }
        bool isCompleted { get; set; }
        int repeatTime { get; set; }
        int flowNodeId { get; set; }
        IEventArg parent { get; }
        EventState state { get; set; }
        [Obsolete]
        EventRecord record { get; set; }
        void setParent(IEventArg parent);
        IEventArg[] getAllChildEvents();
        IEventArg[] getChildEvents(EventState state);
        /// <summary>
        /// 获取环境变量
        /// </summary>
        /// <param name="varName">变量名</param>
        /// <returns>环境变量值</returns>
        object getVar(string varName);
        T getVar<T>(string varName);
        /// <summary>
        /// 设置环境变量
        /// </summary>
        /// <param name="varName">环境变量名</param>
        /// <param name="value">环境变量值</param>
        void setVar(string varName, object value);
        string[] getVarNames();
        void Record(CardEngine game, EventRecord record);
        void addChange(Change change);
        Change[] getChanges();
        Task execute();
        EventDefine define { get; }
        CardEngine game { get; }
    }
    public interface ICardEventDefine
    {
        ICard getCard(IEventArg arg);
    }
    public interface IMassCardEventDefine
    {
        ICard[] getCards(IEventArg arg);
    }
}
