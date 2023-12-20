﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TouhouCardEngine.Interfaces;

namespace TouhouCardEngine
{
    public class GeneratedEventDefine : EventDefine
    {
        public GeneratedEventDefine(ActionReference actionRef) 
        {
            actionReference = actionRef;
        }
        public override Task execute(IEventArg arg)
        {
            var game = arg.game;
            var define = game.getActionDefine(actionReference);
            if (define is not GeneratedActionDefine actionDef)
                return Task.CompletedTask;

            var card = arg.getVar<Card>(VAR_CARD);
            var buff = arg.getVar<Buff>(VAR_BUFF);
            var effect = arg.getVar<IEffect>(VAR_EFFECT);
            var flowEnv = new FlowEnv(game, card, buff, arg, effect);
            Flow flow = new Flow(flowEnv);
            // 为事件设置输入变量。
            setEntryNodeOutputValuesByEventArg(actionDef, flow, arg);
            actionDef.executeGraph(flow);
            // 为事件设置输出变量。
            setEventVariablesByOutputValues(actionDef, flow, arg);
            return Task.CompletedTask;
        }
        /// <summary>
        /// 传递变量值：事件-->入口节点的输出值
        /// </summary>
        /// <param name="arg">事件。</param>
        /// <param name="flow">自定义动作内部的执行流。</param>
        private void setEntryNodeOutputValuesByEventArg(GeneratedActionDefine actionDef, Flow flow, IEventArg arg)
        {
            var entryNode = actionDef.getEntryNode();
            foreach (var input in entryNode.getOutputPorts<ValueOutput>())
            {
                flow.setValue(input, arg.getVar(input.name));
            }
        }
        private void setEventVariablesByOutputValues(GeneratedActionDefine actionDef, Flow flow, IEventArg arg)
        {
            var exitNode = actionDef.getReturnNode();
            foreach (var portDefine in actionDef.getValueOutputs())
            {
                var varName = portDefine.name;
                var inputPort = exitNode.getInputPort<ValueInput>(varName);
                var value = flow.getValue(inputPort);
                arg.setVar(varName, value);
            }
        }
        [Obsolete]
        public override void Record(CardEngine game, EventArg arg, EventRecord record)
        {
            var define = game.getActionDefine(actionReference);
            if (define is not GeneratedActionDefine actionDef)
                return;
            foreach (var varName in actionDef.getAllEventArgVarNames())
            {
                var value = arg.getVar(varName);
                if (value is Card card)
                {
                    record.setCardState(varName, card);
                }
                else if (value is IEnumerable<Card> cards)
                {
                    record.setCardStates(varName, cards);
                }
                else
                {
                    record.setVar(varName, value);
                }
            }
        }
        public override string toString(EventArg arg)
        {
            return $"自定义事件{actionReference}";
        }
        public ActionReference actionReference;
        public const string VAR_CARD = "ENV_CARD";
        public const string VAR_BUFF = "ENV_BUFF";
        public const string VAR_EFFECT = "ENV_EFFECT";
    }
}