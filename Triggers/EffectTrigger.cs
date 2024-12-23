﻿using System.Threading.Tasks;
using TouhouCardEngine.Interfaces;

namespace TouhouCardEngine
{
    public class EffectTrigger : Trigger
    {
        public EffectTrigger(CardEngine game, Card card, Buff buff, Effect effect)
        {
            this.game = game;
            this.card = card;
            this.buff = buff;
            this.effect = effect;
        }
        public override bool checkCondition(IEventArg arg)
        {
            var effectEnv = new EffectEnv(game, card, buff, arg as EventArg, effect);
            return effect.checkCondition(effectEnv);
        }
        public override Task invoke(IEventArg arg)
        {
            var effectEnv = new EffectEnv(game, card, buff, arg as EventArg, effect);
            if (effect is IEventEffect eventEffect)
            {
                return eventEffect.onTrigger(effectEnv);
            }
            return Task.CompletedTask;
        }
        public override int getPriority()
        {
            return effect.getPriority();
        }
        public CardEngine game;
        public Card card;
        public Buff buff;
        public Effect effect;
    }
}
