﻿using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Parts;
using Newtonsoft.Json;
using System.Linq;
using static MicroscopicContentExpansion.Main;

namespace MicroscopicContentExpansion.NewComponents {
    [ComponentName("Crusaders Flurry")]
    [AllowedOn(typeof(BlueprintUnitFact), false)]
    [AllowMultipleComponents]
    [TypeId("c934325c5cbb494f914507761f531ac3")]
    public class CrusadersFlurryUnlock :
        UnitFactComponentDelegate<CrusadersFlurryUnlockData>,
        IUnitActiveEquipmentSetHandler,
        IGlobalSubscriber,
        ISubscriber,
        IUnitEquipmentHandler {

        public BlueprintUnitFactReference _flurryFact1;
        public BlueprintUnitFactReference _flurryFact11;
        public BlueprintUnitFactReference _flurryFact20;
        public BlueprintArchetypeReference _soheiArchetype;
        public BlueprintCharacterClassReference _monkClass;
        public BlueprintFeatureReference _monkFlurry11;
        public BlueprintFeatureReference _soheiFlurry11;
        public BlueprintFeatureReference _oldMaster;
        public BlueprintFeatureSelectionReference _deitySelection;        
        public BlueprintParametrizedFeatureReference _weaponFocus;

        public BlueprintUnitFact Flurry1 => _flurryFact1?.Get();
        public BlueprintUnitFact Flurry11 => _flurryFact11?.Get();
        public BlueprintUnitFact Flurry20 => _flurryFact20?.Get();

        public override void OnActivate() => CheckEligibility();

        public override void OnDeactivate() => RemoveFact();

        public void HandleUnitChangeActiveEquipmentSet(UnitDescriptor unit) => this.CheckEligibility();

        private bool IsDeityMeleeFavoredWeaponWithWeaponFocus() {
            if (Owner.Body.PrimaryHand.MaybeWeapon == null)
                return false;
            var primaryWeapon = Owner.Body.PrimaryHand.Weapon.Blueprint;
            if (primaryWeapon.m_Type.Get().m_AttackType != Kingmaker.RuleSystem.AttackType.Melee)
                return false;
            if (Owner.GetFeature((BlueprintFeature)_weaponFocus, (FeatureParam)primaryWeapon.Category) == null)
                return false;

            if (MCEContext.AddedContent.Feats.IsEnabled("CrusadersFlurryNoGodCheck"))
                return true;

            Owner.Progression.Selections.TryGetValue(_deitySelection?.Get(), out var selection);
            if (selection == null)
                return false;
            var selectedDeity = selection.m_SelectionsByLevel[1].First();
            var comp = selectedDeity.GetComponent<AddStartingEquipment>();
            if (comp == null || comp.m_BasicItems == null || comp.m_BasicItems.Count() == 0)
                return false;
            var favoredWeaponType = ((BlueprintItemWeapon)comp.m_BasicItems[0].Get()).m_Type;
            if (primaryWeapon.m_Type.Equals(favoredWeaponType)) {
                return true;
            }

            return false;
        }

        private void CheckEligibility() {
            var isSoheiArchetype = Owner.Progression.IsArchetype(_soheiArchetype.Get());

            if (!isSoheiArchetype) {
                if (!Owner.Body.SecondaryHand.HasShield && (!Owner.Body.Armor.HasArmor || !Owner.Body.Armor.Armor.Blueprint.IsArmor)
                    && !Owner.Body.PrimaryHand.Weapon.Blueprint.IsMonk && IsDeityMeleeFavoredWeaponWithWeaponFocus()) {
                    AddFact();
                } else
                    RemoveFact();
            } else {
                if (!isSoheiArchetype)
                    return;
                bool flag1 = false;
                if (!Owner.Body.Armor.HasArmor)
                    flag1 = true;
                else if (Owner.Body.Armor.MaybeArmor != null)
                    flag1 = !Owner.Body.Armor.MaybeArmor.Blueprint.IsArmor || Owner.Body.Armor.MaybeArmor.Blueprint.ProficiencyGroup == ArmorProficiencyGroup.Light;
                bool flag2 = Owner.Get<UnitPartWeaponTraining>() != null ? Owner.Body.PrimaryHand.MaybeWeapon.Blueprint.IsMonk ||
                    Owner.Get<UnitPartWeaponTraining>().IsSuitableWeapon(Owner.Body.PrimaryHand.MaybeWeapon)
                    : Owner.Body.PrimaryHand.MaybeWeapon.Blueprint.IsMonk;
                if (!Owner.Body.SecondaryHand.HasShield && flag1 && !flag2 && IsDeityMeleeFavoredWeaponWithWeaponFocus())
                    AddFact();
                else
                    RemoveFact();
            }
        }

        private void AddFact() {
            if (Data.Flurry1 == null) {
                Data.Flurry1 = Owner.AddFact(Flurry1);
            }
            if (Data.Flurry11 == null) {
                if (Owner.HasFact(_monkFlurry11) || Owner.HasFact(_soheiFlurry11)) {
                    Data.Flurry11 = Owner.AddFact(Flurry11);
                }
            }
            if (Data.Flurry20 == null) {
                if (Owner.HasFact(_oldMaster)) {
                    Data.Flurry20 = Owner.AddFact(Flurry20);
                }
            }
        }

        private void RemoveFact() {
            if (Data.Flurry1 != null) {
                Owner.RemoveFact(Data.Flurry1);
                Data.Flurry1 = null;
            }
            if (Data.Flurry11 != null) {
                Owner.RemoveFact(Data.Flurry11);
                Data.Flurry11 = null;
            }
            if (Data.Flurry20 != null) {
                Owner.RemoveFact(Data.Flurry20);
                Data.Flurry20 = null;
            }
        }

        public void HandleEquipmentSlotUpdated(ItemSlot slot, ItemEntity previousItem) {
            if ((UnitEntityData)slot.Owner != (UnitDescriptor)Owner || !slot.Active)
                return;
            CheckEligibility();
        }
    }

    public class CrusadersFlurryUnlockData {
        [JsonProperty]
        public EntityFact Flurry1;
        [JsonProperty]
        public EntityFact Flurry11;
        [JsonProperty]
        public EntityFact Flurry20;
    }
}
