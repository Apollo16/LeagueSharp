using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

namespace LeBlanc_2
{
    internal class LeBlanc
    {
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static readonly Menu LeBlancConfig = Configs.LeBlancConfig;
        private static readonly bool PacketCast = LeBlancConfig.Item("UsePacket").GetValue<bool>();
        private static Spell Q { get { return Spells.Q; } }
        private static Spell W { get { return Spells.W; } }
        private static Spell E { get { return Spells.E; } }
        private static Spell R { get { return Spells.R; } }

        public static void Init()
        {
            Game.OnUpdate += OnGameUpdate;
            Interrupter2.OnInterruptableTarget += OnPossibleToInterrupt;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
        }

        private static void OnGameUpdate(EventArgs args)
        {
            if (args == null || Player.IsDead || Player.IsRecalling())
                return;

            switch (Configs.Orbwalker.ActiveMode)
            {
                    case Orbwalking.OrbwalkingMode.Combo:
                {
                    var t = GetEnemy(1200);
                    if (t != null)
                        GapClose(t);
                    break;
                }
                    case Orbwalking.OrbwalkingMode.LaneClear:
                {
                    LaneClear();
                    break;
                }
                    case Orbwalking.OrbwalkingMode.Mixed:
                {
                    Harass();
                    break;
                }
                    case Orbwalking.OrbwalkingMode.None:
                {
                    Flee();
                    break;
                }
            }

            Other();

            if (LeBlancConfig.Item("haraKey").GetValue<KeyBind>().Active)
                Harass();
        }

        private static void Other()
        {
            if (W.Instance.Name == "LeblancSlide")
                return;

            var wposex = Objects.SecondW.Pos.Extend(Game.CursorPos, 100);
            var fleepos = wposex.Distance(Objects.SecondW.Pos) > Game.CursorPos.Distance(Objects.SecondW.Pos);
            var useW = LeBlancConfig.SubMenu("Misc").SubMenu("backW").Item("SWpos").GetValue<bool>();

            if (fleepos && useW)
            {
                W.Cast(Player, PacketCast);
            }
        }

        #region Combo
        private static void GapClose(Obj_AI_Base t)
        {
            if (t.IsInvulnerable || !t.IsTargetable)
                return;

            var useW = LeBlancConfig.SubMenu("Combo").Item("useW").GetValue<bool>();

            if (!Player.IsKillable(t, new[] { Tuple.Create(SpellSlot.Q, 0), Tuple.Create(SpellSlot.E, 0), Tuple.Create(SpellSlot.R, 0) }) || Q.IsInRange(t) || !useW)
            {
                Combo(t);
                return;
            }

            var vecW = Player.ServerPosition.Extend(t.ServerPosition, W.Range);
            if (W.Instance.Name == "LeblancSlide")
            {
                W.Cast(vecW, PacketCast);
                Combo(t);
            }
            else
            {
                Combo(t);
            }
        }
        private static void Combo(Obj_AI_Base t)
        {
            var useQ = LeBlancConfig.SubMenu("Combo").Item("useQ").GetValue<bool>();
            var useW = LeBlancConfig.SubMenu("Combo").Item("useW").GetValue<bool>();
            var useE = LeBlancConfig.SubMenu("Combo").Item("useE").GetValue<bool>();
            var useR = LeBlancConfig.SubMenu("Combo").Item("useR").GetValue<bool>();
            var hitE = (HitChance)(LeBlancConfig.SubMenu("Combo").Item("preE").GetValue<StringList>().SelectedIndex + 3);
            var hitR = (HitChance)(LeBlancConfig.SubMenu("Combo").Item("preE").GetValue<StringList>().SelectedIndex + 3);
            
            if (useE && t.IsValidTarget(E.Range))
                E.CastIfHitchanceEquals(t, hitE, PacketCast);
            if (useQ && t.IsValidTarget(Q.Range))
                Q.CastOnUnit(t, PacketCast);
            if (useW && !Q.IsReady() && t.IsValidTarget(W.Range) && W.Instance.Name == "LeblancSlide")
                W.Cast(t, PacketCast);
            if (useR)
            {
                switch (Player.Spellbook.GetSpell(SpellSlot.R).Name)
                {
                    case "LeblancChaosOrbM":
                    {
                        if (W.Level <= Q.Level || (!Q.IsReady() && !W.IsReady()) || !Spells.W.IsInRange(t))
                        {
                            R.CastOnUnit(t, PacketCast);
                        }
                        break;
                    }

                    case "LeblancSlideM":
                    {
                        if (t.IsValidTarget(Spells.W.Range) && W.Level > Q.Level)
                        {
                            R.Cast(t, PacketCast);
                        }
                        break;
                    }

                    case "LeblancSoulShackleM":
                    {
                        if ((!Q.IsReady() && !W.IsReady() && !E.IsReady()))
                        {
                            R.CastIfHitchanceEquals(t, hitR, PacketCast);
                        }
                        break;
                    }
                    case "leblancslidereturnm":
                    {
                        R.Cast(Player, PacketCast);
                        break;
                    }
                }
            }
        }
        #endregion Combo
        #region LaneClear

        private static void LaneClear()
        {
            var useQ = LeBlancConfig.Item("LaneClearQ").GetValue<bool>();
            var useW = LeBlancConfig.Item("LaneClearW").GetValue<bool>();
            var minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.NotAlly);
            var minionsJung = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral);
            var farmLocation = MinionManager.GetBestCircularFarmLocation(MinionManager.GetMinions(W.Range).Select(m => m.ServerPosition.To2D()).ToList(), W.Width, W.Range);
            var mana = ObjectManager.Player.ManaPercentage() > LeBlancConfig.Item("LaneClearManaPercent").GetValue<Slider>().Value;
            var minionHit = farmLocation.MinionsHit >= LeBlancConfig.Item("LaneClearWHit").GetValue<Slider>().Value;
            if (!mana)
            {
                return;
            }
            foreach (var minion in minions)
            {
                if (minion != null && minion.IsValidTarget() && minion.Health <= Q.GetDamage(minion) && useQ)
                {
                    Q.CastOnUnit(minion, PacketCast);
                }
            }
            foreach (var minion in minionsJung)
            {
                if (minion != null && minion.IsValidTarget() && useQ)
                {
                    Q.CastOnUnit(minion, PacketCast);
                }
            }

            if (minionHit && useW && W.Instance.Name == "LeblancSlide")
            {
                W.Cast(farmLocation.Position, PacketCast);
            }
            if (farmLocation.MinionsHit > 0 && useW && W.Instance.Name == "LeblancSlide")
            {
                W.Cast(farmLocation.Position, PacketCast);
            }

            if (W.Instance.Name != "LeblancSlide" && LeBlancConfig.Item("LaneClear2W").GetValue<bool>())
                W.Cast(Player, PacketCast);
        }
        #endregion LaneClear
        #region Harass
        private static void Harass()
        {
            var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            var mana = ObjectManager.Player.ManaPercentage() >
                       LeBlancConfig.Item("HarassManaPercent").GetValue<Slider>().Value;
            var useQ = LeBlancConfig.Item("useQ").GetValue<bool>();
            var useW = LeBlancConfig.Item("useW").GetValue<bool>();
            var useE = LeBlancConfig.Item("useE").GetValue<bool>();

            if (!mana || t == null)
            {
                return;
            }

            if (useE)
                E.CastIfHitchanceEquals(t, HitChance.VeryHigh, PacketCast);
            if (useQ)
                Q.CastOnUnit(t, PacketCast);
            if (useW && W.Instance.Name == "LeblancSlide" && !Q.IsReady())
                W.Cast(t, PacketCast);
            if (LeBlancConfig.Item("use2W").GetValue<bool>() && W.Instance.Name != "LeblancSlide")
                W.Cast(Player, PacketCast);
        }
        #endregion Harass
        #region Flee
        private static void Flee()
        {
            if (!LeBlancConfig.Item("FleeK").GetValue<KeyBind>().Active) { return; }

            ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            var useW = LeBlancConfig.Item("FleeW").GetValue<bool>();
            var useE = LeBlancConfig.Item("FleeE").GetValue<bool>();
            var useR = LeBlancConfig.Item("FleeR").GetValue<bool>();

            if (W.IsReady() && useW && W.Instance.Name == "LeblancSlide")
            {
                W.Cast(Game.CursorPos, PacketCast);
            }
            if (R.IsReady() && useR && R.Instance.Name == "LeblancSlideM")
            {
                R.Cast(Game.CursorPos, PacketCast);
            }
            if (useE && t != null)
                E.CastIfHitchanceEquals(t, HitChance.Medium, PacketCast);
        }

        #endregion Flee

        private static void OnPossibleToInterrupt(Obj_AI_Base unit, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (!Configs.LeBlancConfig.Item("Interrupt").GetValue<bool>() || args.DangerLevel != Interrupter2.DangerLevel.High || unit == null)
                return;

            E.CastIfHitchanceEquals(unit, HitChance.Medium);

            if (R.Instance.Name == "LeblancSoulShackleM")
            {
                R.CastIfHitchanceEquals(unit, HitChance.Medium);
            }
        }

        private static void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!Configs.LeBlancConfig.Item("Interrupt").GetValue<bool>())
                return;

            E.CastIfHitchanceEquals(gapcloser.Sender, HitChance.Medium);

            if (R.Instance.Name == "LeblancSoulShackleM")
            {
                R.CastIfHitchanceEquals(gapcloser.Sender, HitChance.Medium);
            }
        }

        private static Obj_AI_Hero GetEnemy(float vDefaultRange = 0, TargetSelector.DamageType vDefaultDamageType = TargetSelector.DamageType.Magical)
        {
            if (Math.Abs(vDefaultRange) < 0.00001)
                vDefaultRange = E.Range;

            if (!LeBlancConfig.Item("AssassinActive").GetValue<bool>())
                return TargetSelector.GetTarget(vDefaultRange, vDefaultDamageType);

            var assassinRange = LeBlancConfig.Item("AssassinSearchRange").GetValue<Slider>().Value;

            var vEnemy = ObjectManager.Get<Obj_AI_Hero>()
                .Where(
                    enemy =>
                        enemy.Team != ObjectManager.Player.Team && !enemy.IsDead && enemy.IsVisible &&
                        LeBlancConfig.Item("Assassin" + enemy.ChampionName) != null &&
                        LeBlancConfig.Item("Assassin" + enemy.ChampionName).GetValue<bool>() &&
                        ObjectManager.Player.Distance((Obj_AI_Base)enemy) < assassinRange);

            if (LeBlancConfig.Item("AssassinSelectOption").GetValue<StringList>().SelectedIndex == 1)
            {
                vEnemy = (from vEn in vEnemy select vEn).OrderByDescending(vEn => vEn.MaxHealth);
            }

            Obj_AI_Hero[] objAiHeroes = vEnemy as Obj_AI_Hero[] ?? vEnemy.ToArray();

            Obj_AI_Hero t = !objAiHeroes.Any()
                ? TargetSelector.GetTarget(vDefaultRange, vDefaultDamageType)
                : objAiHeroes[0];

            return t;
        }

    }
}
