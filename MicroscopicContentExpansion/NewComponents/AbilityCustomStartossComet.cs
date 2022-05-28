﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.Utility;
using Owlcat.Runtime.Core.Logging;
using UnityEngine;


namespace MicroscopicContentExpansion.NewComponents {
    internal class AbilityCustomStartossComet : AbilityCustomLogic {
        public int VitalStrikeMod;
        [SerializeField]
        public BlueprintFeatureReference m_MythicBlueprint;
        [SerializeField]
        public BlueprintFeatureReference m_RowdyFeature;

        public BlueprintFeature MythicBlueprint => this.m_MythicBlueprint?.Get();

        public BlueprintFeature RowdyFeature => this.m_RowdyFeature?.Get();

        public Feet CleaveRadius = 30.Feet();

        private LogChannel logChannel = LogChannelFactory.GetOrCreate("Mods");

        public override IEnumerator<AbilityDeliveryTarget> Deliver(AbilityExecutionContext context, TargetWrapper target) {
            logChannel.Error("Startoss comet is a go!");
            var caster = context.MaybeCaster;
            var previousTarget = target.Unit;
            if (caster == null) {
                PFLog.Default.Error(this, "Caster is missing", Array.Empty<object>());
                logChannel.Error("Startoss comet Caster is missing!");
                yield break;
            }
            bool isGreater = true;
            bool isMythic = true;
            var threatHand = caster.GetThreatHandRanged();
            if (threatHand == null) {
                PFLog.Default.Error("Caster can't attack", Array.Empty<object>());
                logChannel.Error("Startoss comet Threat hand NULL!");
                yield break;
            }
            var targetUnit = target.Unit;
            if (targetUnit == null) {
                PFLog.Default.Error("Can't be applied to point", Array.Empty<object>());
                logChannel.Error("Startoss comet Can't be applied to point!");
                yield break;
            }
            List<UnitEntityData> targetList = new List<UnitEntityData>
            {
                targetUnit
            };
            logChannel.Log("Starting to count enemies");
            foreach (UnitGroupMemory.UnitInfo unitInfo in caster.Memory.Enemies) {
                UnitEntityData unit = unitInfo.Unit;
                if (unit != targetUnit && unit.Descriptor.State.IsConscious && caster.IsReach(unit, threatHand)) {
                    targetList.Add(unit);
                }
            }
            logChannel.Log($"Found: {targetList.Count} enemies");
            targetList.Sort((UnitEntityData u1, UnitEntityData u2) => u1.DistanceTo(targetUnit).CompareTo(u2.DistanceTo(targetUnit)));
            List<UnitEntityData> hitTargets = new List<UnitEntityData>();
            List<UnitEntityData> validTargets = new List<UnitEntityData>(targetList);
            while (validTargets.Count > 0) {
                if (!isMythic) {
                    validTargets
                        .Sort((UnitEntityData u1, UnitEntityData u2) => u1.DistanceTo(previousTarget).CompareTo(u2.DistanceTo(previousTarget)));
                    validTargets = validTargets
                        .Where(t => t.DistanceTo(previousTarget) <= (t.View.Corpulence + previousTarget.View.Corpulence + CleaveRadius.Meters))
                        .ToList();
                }
                var currentTarget = validTargets.FirstOrDefault();
                if (currentTarget == null) { break; }
                logChannel.Log($"Found valid enemy");
                var chainSource = previousTarget != currentTarget ? previousTarget : null;
                var res = context.TriggerRule(new RuleAttackWithWeaponChaining(caster, chainSource, currentTarget, threatHand.Weapon, 0) {
                    IsFirstAttack = hitTargets.Any()
                });

                if (!res.AttackRoll.IsHit) {
                    break;
                }
                while (res.LaunchedProjectiles.Any(t => !t.IsHit)) {
                    if (res.LaunchedProjectiles.Any(t => t.Cleared)) {
                        yield break;
                    }
                    yield return null;
                }

                var startTime = Game.Instance.TimeController.GameTime;
                while (Game.Instance.TimeController.GameTime - startTime < ((float)0.2f).Seconds())
                    yield return null;

                hitTargets.Add(currentTarget);
                previousTarget = currentTarget;
                yield return new AbilityDeliveryTarget(currentTarget);
                if (!isGreater && hitTargets.Count > 1) {
                    break;
                }
                validTargets = targetList.Where(t => !hitTargets.Contains(t)).ToList();                
            }
            yield break;
        }

        public override void Cleanup(AbilityExecutionContext context) {
        }

        private class EventHandlers : IDisposable {
            private readonly List<object> m_Handlers = new List<object>();

            public void Add(object handler) => this.m_Handlers.Add(handler);

            public AbilityCustomStartossComet.EventHandlers Activate() {
                foreach (object handler in this.m_Handlers)
                    EventBus.Subscribe(handler);
                return this;
            }

            public void Dispose() {
                foreach (object handler in this.m_Handlers)
                    EventBus.Unsubscribe(handler);
            }
        }

        public class VitalStrike :
          IInitiatorRulebookHandler<RuleCalculateWeaponStats>,
          IRulebookHandler<RuleCalculateWeaponStats>,
          ISubscriber,
          IInitiatorRulebookSubscriber {
            private readonly UnitEntityData m_Unit;
            private int m_DamageMod;
            private bool m_Mythic;
            private bool m_Rowdy;

            public VitalStrike(UnitEntityData unit, int damageMod, bool mythic, bool rowdy) {
                this.m_Unit = unit;
                this.m_DamageMod = damageMod;
                this.m_Mythic = mythic;
                this.m_Rowdy = rowdy;
            }

            public UnitEntityData GetSubscribingUnit() => this.m_Unit;

            public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt) {
            }

            public void OnEventDidTrigger(RuleCalculateWeaponStats evt) {
                DamageDescription damageDescription1 = evt.DamageDescription.FirstItem<DamageDescription>();
                if (damageDescription1 == null || damageDescription1.TypeDescription.Type != DamageType.Physical)
                    return;
                damageDescription1.Dice = new DiceFormula(damageDescription1.Dice.Rolls * this.m_DamageMod, damageDescription1.Dice.Dice);
                if (this.m_Mythic)
                    damageDescription1.Bonus *= this.m_DamageMod;
                if (!this.m_Rowdy || evt.Initiator.Descriptor.Stats.SneakAttack.ModifiedValue <= 0)
                    return;
                DamageDescription damageDescription2 = new DamageDescription();
                DamageTypeDescription typeDescription = evt.DamageDescription.FirstItem<DamageDescription>().TypeDescription;
                damageDescription2.TypeDescription = new DamageTypeDescription() {
                    Common = new DamageTypeDescription.CommomData() {
                        Alignment = typeDescription.Common.Alignment,
                        Precision = true,
                        Reality = typeDescription.Common.Reality
                    },
                    Energy = typeDescription.Energy,
                    Physical = new DamageTypeDescription.PhysicalData() {
                        Enhancement = typeDescription.Physical.Enhancement,
                        EnhancementTotal = typeDescription.Physical.EnhancementTotal,
                        Form = typeDescription.Physical.Form,
                        Material = typeDescription.Physical.Material
                    },
                    Type = typeDescription.Type
                };
                damageDescription2.Dice = new DiceFormula(2 * evt.Initiator.Descriptor.Stats.SneakAttack.ModifiedValue, DiceType.D6);
                evt.DamageDescription.Add(damageDescription2);
            }
        }
    }
}