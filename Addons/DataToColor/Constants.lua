local Load = select(2, ...)
local DataToColor = unpack(Load)

DataToColor.C.unitPlayer = "player"
DataToColor.C.unitTarget = "target"
DataToColor.C.unitPet = "pet"
DataToColor.C.unitPetTarget = "pettarget"
DataToColor.C.unitTargetTarget = "targettarget"
DataToColor.C.unitNormal = "normal"

-- Creature Types
DataToColor.C.Humanoid = "Humanoid"
DataToColor.C.Elemental = "Elemental"
DataToColor.C.Mechanical = "Mechanical"
DataToColor.C.Totem = "Totem"

-- Character's name
DataToColor.C.CHARACTER_NAME = UnitName(DataToColor.C.unitPlayer)
DataToColor.C.CHARACTER_GUID = UnitGUID(DataToColor.C.unitPlayer)
_, DataToColor.C.CHARACTER_CLASS, DataToColor.C.CHARACTER_CLASS_ID = UnitClass(DataToColor.C.unitPlayer)
_, _, DataToColor.C.CHARACTER_RACE_ID = UnitRace(DataToColor.C.unitPlayer)

-- Actionbar power cost
DataToColor.C.MAX_POWER_TYPE = 1000000
DataToColor.C.MAX_ACTION_IDX = 1000

-- Spells
DataToColor.C.Spell.AutoShotId = 75 -- Auto shot
DataToColor.C.Spell.ShootId = 5019 -- Shoot
DataToColor.C.Spell.AttackId = 6603 -- Attack

-- Item / Inventory
DataToColor.C.ItemPattern = "(m:%d+)"

-- Gossips
DataToColor.C.Gossip = {
    ["banker"] = 0,
    ["battlemaster"] = 1,
    ["binder"] = 2,
    ["gossip"] = 3,
    ["healer"] = 4,
    ["petition"] = 5,
    ["tabard"] = 6,
    ["taxi"] = 7,
    ["trainer"] = 8,
    ["unlearn"] = 9,
    ["vendor"] = 10,
}

-- Mirror timer labels
DataToColor.C.MIRRORTIMER.BREATH = "BREATH"

DataToColor.C.ActionType.Spell = "spell"
DataToColor.C.ActionType.Macro = "macro"