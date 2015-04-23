using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;



namespace Diana
{
    class Program
    {
        public const string ChampionName = "Diana";
        public static Spell Q, W, E, R;
        public static float Qmana, Wmana, Emana, Rmana;
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);
        public static Items.Item Youmuu = new Items.Item(3142, 0);
        public static Orbwalking.Orbwalker Orbwalker;
        public static List<Spell> SpellList = new List<Spell>();
        public static Obj_AI_Hero Player;
        public static Helper Helper;
        //public static readonly Obj_AI_Hero player = ObjectManager.Player;
        public static Menu Config;

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;
            if (Player.BaseSkinName != ChampionName) return;
            Q = new Spell(SpellSlot.Q, 830f); //830\\
            W = new Spell(SpellSlot.W, 200f); //200\\--[leagueoflegends.wikia.com/wiki/Diana]
            E = new Spell(SpellSlot.E, 350f); //350\\--[  Last checked: 5.7  |  21-04-2015  ]
            R = new Spell(SpellSlot.R, 825f); //825\\ 
            Q.SetSkillshot(0.25f, 195f, 1600, false, SkillshotType.SkillshotCircle);
            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            Config = new Menu(ChampionName, ChampionName, true);
            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();
            Config.AddItem(new MenuItem("Hit", "Hit Chance Q").SetValue(new Slider(3, 4, 0)));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("Haras Q").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));
            Config.AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.SubMenu("E Moonfall Config").AddItem(new MenuItem("AGC", "Auto E Multiple Targets").SetValue(true));
            Config.SubMenu("E Moonfall Config").AddItem(new MenuItem("interruptE", "Auto CC Channeling").SetValue(true));
            Config.SubMenu("E Moonfall Config").AddItem(new MenuItem("Edmg", "E dmg % hp").SetValue(new Slider(0, 100, 0)));
            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));

            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            PotionMenager();
            if (Player.CountEnemiesInRange(1700) <= 2) {
                if (Q.IsReady()) {
                    var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
                    if (t.IsValidTarget()) {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo 
                            && Player.Mana > Rmana + Qmana)
                            CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear 
                            && Config.Item("haras" + t.BaseSkinName).GetValue<bool>())
                            //if (Player.Mana > Rmana + Wmana + Qmana + Qmana && t.Path.Count() > 1)
                                CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                            //if (Player.Mana > Player.MaxMana * 0.9)
                                //CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                            //if (HasUltimate() && (Player.Mana > Rmana + Wmana + Qmana + Qmana))
                                //Q.CastIfWillHit(t, 2, true);
                        if (Player.Mana > Rmana + Qmana + Wmana && Q.IsReady()) {
                            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range))) {
                                if (enemy.HasBuffOfType(BuffType.Stun)
                                    || enemy.HasBuffOfType(BuffType.Snare)
                                    || enemy.HasBuffOfType(BuffType.Charm)
                                    || enemy.HasBuffOfType(BuffType.Fear) 
                                    || enemy.HasBuffOfType(BuffType.Taunt) 
                                    || enemy.HasBuffOfType(BuffType.Slow) 
                                    || enemy.HasBuff("Recall"))
                                    Q.Cast(enemy, true);
                                Q.CastIfHitchanceEquals(enemy, HitChance.Immobile, true);
                            }
                        }
                    }
                }
                if (W.IsReady()) {
                    var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
                    var buff = Player.Buffs.Find(x => x.Name == "MinionAggro");
                    if (t.IsValidTarget()) {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo
                            && Player.Mana >= Wmana)
                            CastSpell(W, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear
                            && Config.Item("haras" + t.BaseSkinName).GetValue<bool>()
                            && Player.Mana > Wmana
                            && (buff != null)
                            && buff.Count >= 4) {
                            CastSpell(W, t, Config.Item("Hit").GetValue<Slider>().Value);
                        }
                    }
                }
                if (E.IsReady()) {
                    var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
                    if (t.IsValidTarget()) {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Player.Mana >= Emana)
                            CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (t.CountEnemiesInRange(E.Range) > 0) {
                        
                        }
                    }
                }
                if (R.IsReady()) {
                    var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Magical);
                    if (t.IsValidTarget()) {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo 
                            && Player.Mana >= Rmana && t.HasBuff("dianamoonlight"))
                            CastSpell(R, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo 
                            && Player.Mana >= Rmana && t.Health <= R.GetDamage(t))
                            CastSpell(R, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear 
                            && Player.Mana >= Rmana 
                            && t.Health <= R.GetDamage(t)) 
                            Game.NotifyPopUp("Press Combat Key to Kill unit.");
                    }
                }
            }
            if (Player.CountEnemiesInRange(1700) >= 3) {
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
                if (Q.IsReady()) {
                    if (t.IsValidTarget())
                    {
                        MultipleQ();
                    }
                }
                if (W.IsReady()){}
                if (E.IsReady()){}
                if (R.IsReady())
                {
                    if (Player.CountAlliesInRange(1700) >= 2 || ImFed() ||
                        ISelectedTargetWithLeftMouseClick() && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Config.Item("AGC" + t.BaseSkinName).GetValue<bool>())
                    {
                        
                    }
                }
            }
        }

        static void MultipleQ()
        {
            var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
                int enemiesHit = 0;
                int killableHits = 0;

                foreach (Obj_AI_Hero enemy in Program.Helper.EnemyTeam.Where(x => t.IsValidTarget(Q.Range)))
                {
                    var prediction = Prediction.GetPrediction(enemy, Q.Delay);

                    if (prediction != null && prediction.UnitPosition.Distance(ObjectManager.Player.ServerPosition) <= Q.Range)
                    {
                        enemiesHit++;

                        if (ObjectManager.Player.GetSpellDamage(enemy, SpellSlot.W) >= enemy.Health)
                            killableHits++;
                    }
                }

                if (enemiesHit >= 2 || (killableHits >= 1 && ObjectManager.Player.Health / ObjectManager.Player.MaxHealth <= 0.1))
                    return;
        }

        private static bool CheckRange(Obj_AI_Hero e)
        {
            if (e.CountEnemiesInRange(750) >= 1 && e.CountEnemiesInRange(750) < 3)
            {
                return true;
            }
            return false;
        }

        private static bool HasUltimate()
        {
            if (R.IsReady())
            {
                return true;
            }
            return false;
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("OrbDraw").GetValue<bool>())
            {
                if (Player.HealthPercent > 60)
                    //Utility.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.GreenYellow, 2, 1);
                    Render.Circle.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.GreenYellow, 2);
                else if (Player.HealthPercent > 30)
                    //Utility.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.Orange, 3, 1);
                    Render.Circle.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.Orange, 3);
                else
                    //Utility.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.Red, 4, 1);
                    Render.Circle.DrawCircle(Player.Position, Player.AttackRange + Player.BoundingRadius * 2, System.Drawing.Color.Red, 4);
            }
            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (Q.IsReady())
                        //Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                    Render.Circle.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1);
                }
                else
                    //Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                    Render.Circle.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1);
            }
            

            if (Config.Item("orb").GetValue<bool>())
            {
                var orbT = Orbwalker.GetTarget();

                if (orbT.IsValidTarget())
                {
                    if (orbT.Health > orbT.MaxHealth * 0.6)
                        //Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.GreenYellow, 5, 1);
                        Render.Circle.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.GreenYellow, 5);
                    else if (orbT.Health > orbT.MaxHealth * 0.3)
                        //Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Orange, 10, 1);
                        Render.Circle.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Orange, 10);
                    else
                        //Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Red, 10, 1);
                        Render.Circle.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Red, 10);
                }

            }


            if (Config.Item("noti").GetValue<bool>())
            {
                var target = TargetSelector.GetTarget(1500, TargetSelector.DamageType.Physical);
                if (target.IsValidTarget())
                {
                    if (Q.GetDamage(target) * 2 > target.Health)
                    {
                        Render.Circle.DrawCircle(target.ServerPosition, 200, System.Drawing.Color.Red);
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.4f, System.Drawing.Color.Red, "Q kill: " + target.ChampionName + " have: " + target.Health + "hp");
                    }
                    
                }
            }
        }

        public static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.Target == null)
                return;
            var dmg = sender.GetSpellDamage(Player, args.SData.Name);
            double HpLeft = Player.Health - dmg;
            double HpPercentage = (dmg * 100) / Player.Health;
            if (sender.IsValid<Obj_AI_Hero>() && HpPercentage >= Config.Item("Edmg").GetValue<Slider>().Value && !sender.IsValid<Obj_AI_Turret>() && sender.IsEnemy && args.Target.IsMe && !args.SData.IsAutoAttack() && Config.Item("autoE").GetValue<bool>() && E.IsReady())
            {
                E.Cast();
                //Game.PrintChat("" + HpPercentage);
            }
            foreach (var target in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (args.Target.NetworkId == target.NetworkId && args.Target.IsEnemy)
                {

                    dmg = sender.GetSpellDamage(target, args.SData.Name);
                     HpLeft = target.Health - dmg;

                    if (!Orbwalking.InAutoAttackRange(target) && target.IsValidTarget(Q.Range) && Q.IsReady())
                    {
                        var qDmg = Q.GetDamage(target);
                        if (qDmg > HpLeft && HpLeft > 0)
                        {
                            Q.Cast(target, true);
                        }
                    }
                    
                }
            }
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var Target = (Obj_AI_Hero)gapcloser.Sender;
            if (Config.Item("AGC").GetValue<bool>() && E.IsReady() && Target.IsValidTarget(1000))
                E.Cast();
            return;
        }

        public static void ManaMenager()
        {
            Qmana = Q.Instance.ManaCost;
            Wmana = W.Instance.ManaCost;
            if (!R.IsReady())
                Rmana = Qmana - Player.Level * 2;
            else
                Rmana = R.Instance.ManaCost;
            if (Player.Health < Player.MaxHealth * 0.3)
            {
                Qmana = 0;
                Wmana = 0;
                Rmana = 0;
            }
        }

        public static void PotionMenager()
        {
            if (Config.Item("pots").GetValue<bool>() && !Player.InFountain() && !Player.HasBuff("Recall"))
            {
                if (Potion.IsReady() && !Player.HasBuff("RegenerationPotion", true))
                {
                    if (Player.CountEnemiesInRange(700) > 0 && Player.Health + 200 < Player.MaxHealth)
                        Potion.Cast();
                    else if (Player.Health < Player.MaxHealth * 0.6)
                        Potion.Cast();
                }
                if (ManaPotion.IsReady() && !Player.HasBuff("FlaskOfCrystalWater", true))
                {
                    if (Player.CountEnemiesInRange(1200) > 0 && Player.Mana < Rmana + Wmana + Qmana)
                        ManaPotion.Cast();
                }
            }
        }

        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            //HitChance 0 - 2
            // example CastSpell(Q, ts, 2);
            if (HitChanceNum == 0)
                QWER.Cast(target, true);
            else if (HitChanceNum == 1)
                QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
            else if (HitChanceNum == 2)
            {
                if (QWER.Delay < 0.3)
                    QWER.CastIfHitchanceEquals(target, HitChance.Dashing, true);
                QWER.CastIfHitchanceEquals(target, HitChance.Immobile, true);
                QWER.CastIfWillHit(target, 2, true);
                if (target.Path.Count() < 2)
                    QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
            }
            else if (HitChanceNum == 3)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                //debug("" + target.Path.Count() + " " + (target.Position == target.ServerPosition) + (waypoints.Last<Vector2>().To3D() == target.ServerPosition));
                if (QWER.Delay < 0.3)
                    QWER.CastIfHitchanceEquals(target, HitChance.Dashing, true);
                QWER.CastIfHitchanceEquals(target, HitChance.Immobile, true);
                QWER.CastIfWillHit(target, 2, true);

                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                else if (target.Path.Count() < 2
                    && (target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) > BackToFront
                    || target.HasBuffOfType(BuffType.Slow) || target.HasBuff("Recall")
                    || (target.Path.Count() == 0 && target.Position == target.ServerPosition)
                    ))
                {
                    if (target.IsFacing(Player) || target.Path.Count() == 0)
                    {
                        if (Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed)))
                            QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                    else
                    {
                        QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }
            }
            else if (HitChanceNum == 4 && (int)QWER.GetPrediction(target).Hitchance > 4)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                //debug("" + target.Path.Count() + " " + (target.Position == target.ServerPosition) + (waypoints.Last<Vector2>().To3D() == target.ServerPosition));
                if (QWER.Delay < 0.3)
                    QWER.CastIfHitchanceEquals(target, HitChance.Dashing, true);
                QWER.CastIfHitchanceEquals(target, HitChance.Immobile, true);
                QWER.CastIfWillHit(target, 2, true);

                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                else if (target.Path.Count() < 2
                    && (target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) > BackToFront
                    || target.HasBuffOfType(BuffType.Slow) || target.HasBuff("Recall")
                    || (target.Path.Count() == 0 && target.Position == target.ServerPosition)
                    ))
                {
                    if (target.IsFacing(Player) || target.Path.Count() == 0)
                    {
                        if (Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed)))
                            QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                    else
                    {
                        QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }
            }
        }
    }
}