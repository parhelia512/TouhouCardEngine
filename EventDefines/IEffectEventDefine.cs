﻿namespace TouhouCardEngine.Interfaces
{
    public interface IEffectEventDefine
    {
        Card getCard(EventArg arg);
        IEffect getEffect(EventArg arg);
    }
}
