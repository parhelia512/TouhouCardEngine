﻿namespace TouhouCardEngine.Histories
{
    public interface IChangeable
    {

    }
    public abstract class Change
    {
        public virtual IChangeable target { get; }
        public void apply() => applyFor(target);
        public void revert() => revertFor(target);
        public abstract void applyFor(IChangeable changeable);
        public abstract void revertFor(IChangeable changeable);
    }
    public abstract class Change<T> : Change where T : IChangeable
    {
        public override sealed IChangeable target => targetGeneric;
        public T targetGeneric { get; }
        public Change(T target)
        {
            targetGeneric = target;
        }
        public override sealed void applyFor(IChangeable changeable)
        {
            if (changeable is T tObj)
            {
                applyFor(tObj);
            }
        }
        public override sealed void revertFor(IChangeable changeable)
        {
            if (changeable is T tObj)
            {
                revertFor(tObj);
            }
        }
        public abstract void applyFor(T changeable);
        public abstract void revertFor(T changeable);
    }
}