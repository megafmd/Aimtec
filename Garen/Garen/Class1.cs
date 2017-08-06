namespace Garen
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using Aimtec;
    using Aimtec.SDK.Damage;
    using Aimtec.SDK.Extensions;
    using Aimtec.SDK.Menu;
    using Aimtec.SDK.Menu.Components;
    using Aimtec.SDK.Orbwalking;
    using Aimtec.SDK.TargetSelector;
    

    using Spell = Aimtec.SDK.Spell;

    internal class Garen
    {
        public static Menu Menu = new Menu("Garen", "Garen", true);

        public static Orbwalker Orbwalker = new Orbwalker();

        public static Obj_AI_Hero Player = ObjectManager.GetLocalPlayer();

        public static Spell Q, W, E, R;
        public void LoadSpells()
        {
            Q = new Spell(SpellSlot.Q,Player.AttackRange);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 325);
            R = new Spell(SpellSlot.R, 400);

        }

        public Garen()
        {
            Orbwalker.Attach(Menu);
            var ComboMenu = new Menu("combo", "Combo");
            {
                ComboMenu.Add(new MenuBool("useq", "Use Q"));
                ComboMenu.Add(new MenuBool("usee", "Use E "));
                ComboMenu.Add(new MenuBool("user", "Use R"));
            }
            Menu.Add(ComboMenu);
            var HarassMenu = new Menu("harass", "Harass");
            {
                HarassMenu.Add(new MenuBool("useq", "Use Q"));
                HarassMenu.Add(new MenuBool("usee", "Use E "));

            }
            Menu.Add(HarassMenu);
            var FarmMenu = new Menu("farming", "Farming");
            {
                FarmMenu.Add(new MenuBool("useq", "Use Q", false));
                FarmMenu.Add(new MenuBool("usee", "Use E"));


            }
            Menu.Add(FarmMenu);
           var KSMenu = new Menu("killsteal", "Killsteal");
            {
                KSMenu.Add(new MenuBool("ksq", "Killsteal with Q"));
                KSMenu.Add(new MenuBool("kse", "Killsteal with E"));
                KSMenu.Add(new MenuBool("ksr", "Killsteal with R", false));
                KSMenu.Add(new MenuBool("ksrgap", "Gapclose with Q for R", false));
            }
            Menu.Add(KSMenu);
            var DrawMenu = new Menu("drawings", "Drawings");
            {
                DrawMenu.Add(new MenuBool("drawq", "Draw Q Range"));
                DrawMenu.Add(new MenuBool("drawe", "Draw E Range"));
                DrawMenu.Add(new MenuBool("drawr", "Draw R Range"));
                DrawMenu.Add(new MenuBool("drawDamage", "Draw Damage"));
            }
            Menu.Add(DrawMenu);

            Menu.Attach();

            Render.OnPresent += Render_OnPresent;
            Game.OnUpdate += Game_OnUpdate;


            LoadSpells();
            Console.WriteLine("Garen by megafmd - Loaded");
        }
        public static readonly List<string> SpecialChampions = new List<string> { "Annie", "Jhin" };
        public static int SxOffset(Obj_AI_Hero target)
        {
            return SpecialChampions.Contains(target.ChampionName) ? 1 : 10;
        }
        public static int SyOffset(Obj_AI_Hero target)
        {
            return SpecialChampions.Contains(target.ChampionName) ? 3 : 20;
        }
        private void Render_OnPresent()
        {

          

            if (Menu["drawings"]["drawe"].Enabled)
            {
                Render.Circle(Player.Position, E.Range, 50, Color.LightGreen);
            }
         
            if (Menu["drawings"]["drawr"].Enabled)
            {
                Render.Circle(Player.Position, R.Range, 50, Color.Crimson);
            }
            if (Menu["drawings"]["drawDamage"].Enabled)
            {

                ObjectManager.Get<Obj_AI_Base>()
                    .Where(h => h is Obj_AI_Hero && h.IsValidTarget() && h.IsValidTarget(R.Range))
                    .ToList()
                    .ForEach(
                        unit =>
                        {

                            var heroUnit = unit as Obj_AI_Hero;
                            int width = 103;
                            int height = 8;
                            int xOffset = SxOffset(heroUnit);
                            int yOffset = SyOffset(heroUnit);
                            var barPos = unit.FloatingHealthBarPosition;
                            barPos.X += xOffset;
                            barPos.Y += yOffset;

                            var drawEndXPos = barPos.X + width * (unit.HealthPercent() / 100);
                            var drawStartXPos = (float)(barPos.X + (unit.Health > Player.GetSpellDamage(unit, SpellSlot.Q) + Player.GetSpellDamage(unit, SpellSlot.E) + Player.GetSpellDamage(unit, SpellSlot.R) + Player.GetSpellDamage(unit, SpellSlot.W)
                                                            ? width * ((unit.Health - Player.GetSpellDamage(unit, SpellSlot.Q) + Player.GetSpellDamage(unit, SpellSlot.E) + Player.GetSpellDamage(unit, SpellSlot.R) + Player.GetSpellDamage(unit, SpellSlot.W)) / unit.MaxHealth * 100 / 100)
                                                            : 0));

                            Render.Line(drawStartXPos, barPos.Y, drawEndXPos, barPos.Y, height, true, unit.Health < Player.GetSpellDamage(unit, SpellSlot.Q) + Player.GetSpellDamage(unit, SpellSlot.E) + Player.GetSpellDamage(unit, SpellSlot.R) + Player.GetSpellDamage(unit, SpellSlot.W) ? Color.GreenYellow : Color.Orange);

                        });
            }
        }

           
        private void Game_OnUpdate()
        {
            if (Player.IsDead || MenuGUI.IsChatOpen())
            {
                return;
            }

              
            switch (Orbwalker.Mode)
            {
                case OrbwalkingMode.Combo:
                    OnCombo();
                    break;
                case OrbwalkingMode.Mixed:
                    OnHarass();
                    break;
                case OrbwalkingMode.Laneclear:
                    Clearing();
                    Jungle();
                    break;

            }


            Killsteal();
        }
        public static List<Obj_AI_Minion> GetEnemyLaneMinionsTargets()
        {
            return GetEnemyLaneMinionsTargetsInRange(float.MaxValue);
        }
        public static List<Obj_AI_Minion> GetEnemyLaneMinionsTargetsInRange(float range)
        {
            return GameObjects.EnemyMinions.Where(m => m.IsValidTarget(float.MaxValue)).ToList();
        }

        private void Clearing()
        {
            bool useQ = Menu["farming"]["useq"].Enabled;
            bool useE = Menu["farming"]["usee"].Enabled;
           
            {
                if (useQ)
                {
                    foreach (var minion in GetEnemyLaneMinionsTargetsInRange(float.MaxValue))
                    {

                        if (minion.IsValidTarget(R.Range) && minion != null)
                        {
                            Q.Cast(minion);
                        }
                    }
                }
               
                if (useE)
                {
                    foreach (var minion in GetEnemyLaneMinionsTargetsInRange(E.Range))
                    {

                        if (minion.IsValidTarget(E.Range) && minion != null)
                        {
                            E.Cast(minion);
                        }
                    }
                }


            }
        }
     
        public static List<Obj_AI_Minion> GetGenericJungleMinionsTargets()
        {
            return GetGenericJungleMinionsTargetsInRange(float.MaxValue);
        }
        public static List<Obj_AI_Minion> GetGenericJungleMinionsTargetsInRange(float range)
        {
            return GameObjects.Jungle.Concat(GameObjects.JungleSmall).Where(m => m.IsValidTarget(range)).ToList();
        }

        private void Jungle()
        {
            foreach (var jungleTarget in GameObjects.Jungle.Where(m => m.IsValidTarget(R.Range)).ToList())
            {
                if (!jungleTarget.IsValidTarget() ||
                    !GetGenericJungleMinionsTargets().Contains(jungleTarget))
                {
                    return;
                }
                bool useQ = Menu["farming"]["useq"].Enabled;
               // bool useW = Menu["farming"]["usew"].Enabled;
                bool useE = Menu["farming"]["usee"].Enabled;
               
                {
                    if (useQ && jungleTarget.IsValidTarget(R.Range))
                    {
                        Q.Cast(jungleTarget);
                    }
                    //if (useW && jungleTarget.IsValidTarget(W.Range) && !Player.HasBuff("AuraofDespair"))
                   // {
                       // W.Cast();
                   // }
                    if (useE && jungleTarget.IsValidTarget(E.Range))
                    {
                        E.Cast(jungleTarget);
                    }
                }
            }
        }
                       
        public static Obj_AI_Hero GetBestKillableHero(Spell spell, DamageType damageType = DamageType.True, bool ignoreShields = false)
        {
            return TargetSelector.Implementation.GetOrderedTargets(spell.Range).FirstOrDefault(t => t.IsValidTarget());
        }
        public static Obj_AI_Hero GetRGAP(DamageType damageType = DamageType.True, bool ignoreShields = false)
        {
            return TargetSelector.Implementation.GetOrderedTargets(R.Range).FirstOrDefault(t => t.IsValidTarget());
        }
        private void Killsteal()

        {
            if (Q.Ready &&
                Menu["killsteal"]["ksq"].Enabled)
            {
               var bestTarget = GetBestKillableHero(Q, DamageType.Physical, false);
                if (bestTarget != null &&
                   Player.GetSpellDamage(bestTarget, SpellSlot.Q) >= bestTarget.Health && bestTarget.IsValidTarget())
               {
                    Q.CastOnUnit(bestTarget);
                }
            }
            if (E.Ready &&
                Menu["killsteal"]["kse"].Enabled)
            {
                var bestTarget = GetBestKillableHero(E, DamageType.Physical, false);
                if (bestTarget != null &&
                    Player.GetSpellDamage(bestTarget, SpellSlot.E) >= bestTarget.Health && bestTarget.IsValidTarget(E.Range))
                {
                    E.Cast(bestTarget);
                }
            }
            if (R.Ready &&
                Menu["killsteal"]["ksr"].Enabled)
            {
                var bestTarget = GetBestKillableHero(R, DamageType.Physical, false);
                if (bestTarget != null &&
                    Player.GetSpellDamage(bestTarget, SpellSlot.R) >= bestTarget.Health && bestTarget.IsValidTarget(R.Range))
                {
                    R.Cast(bestTarget);
                }
            }
            if (Q.Ready &&
                Menu["killsteal"]["ksrgap"].Enabled)
            {
                var bestTarget = GetRGAP(DamageType.Physical, false);
                if (bestTarget != null &&
                    Player.GetSpellDamage(bestTarget, SpellSlot.R) >= bestTarget.Health && bestTarget.Distance(Player) > R.Range)
                {
                    Q.Cast(bestTarget.Position);

                }
                if (bestTarget != null && bestTarget.Distance(Player) <= R.Range && bestTarget != null && Player.GetSpellDamage(bestTarget, SpellSlot.R) >= bestTarget.Health)
                {
                    R.Cast();
                }
            }
        }

        public static Obj_AI_Hero GetBestEnemyHeroTarget()
        {
            return GetBestEnemyHeroTargetInRange(float.MaxValue);
        }

        public static Obj_AI_Hero GetBestEnemyHeroTargetInRange(float range)
        {
            var ts = TargetSelector.Implementation;
            var target = ts.GetTarget(range);
            if (target != null && target.IsValidTarget())
            {
                return target;
            }

            return ts.GetOrderedTargets(range).FirstOrDefault(t => target.IsValidTarget());
        }
        private void OnCombo()
        {
            bool useQ = Menu["combo"]["useq"].Enabled;
            bool useE = Menu["combo"]["usee"].Enabled;
            bool useR = Menu["combo"]["user"].Enabled;
           

            var target = GetBestEnemyHeroTargetInRange(500);
            if (!target.IsValidTarget())
            {
                return;
            }

            if (Q.Ready && useQ && target.IsValidTarget())
            {
                if (target != null)
                {
                    Q.Cast(target);

                    if (!Q.Ready)
                    {
                        if (E.Ready)
                        {
                            E.Cast(target);
                        }
                    }

                }
            }



           // if (E.Ready && useE && target.IsValidTarget(150))
            //{
               // if (target != null)
               // {
                 //   E.Cast();
               // }
          //  }
            if (R.Ready && useR && target.IsValidTarget(R.Range))
            {
                if (target != null)
                {
                    R.Cast();
                }
            }
        }

        private void OnHarass()
        {
            bool useQ = Menu["harass"]["useq"].Enabled;
            //bool useW = Menu["harass"]["usew"].Enabled;
            bool useE = Menu["harass"]["usee"].Enabled;

            var target = GetBestEnemyHeroTargetInRange(R.Range);
           
            {
                if (!target.IsValidTarget())
                {
                    return;
                }
                if (Q.Ready && useQ && target.IsValidTarget(R.Range))
                {
                    if (target != null)
                    {
                        Q.CastOnUnit(target);
                    }
                }
             
                if (E.Ready && useE && target.IsValidTarget(E.Range))
                {
                    if (target != null)
                    {
                        E.Cast();
                    }
                }
            }
        }
    }
}