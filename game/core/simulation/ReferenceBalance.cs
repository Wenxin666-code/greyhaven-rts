using System;
using System.Drawing;

namespace Greyhaven {
public enum ArmorKind { None, Flak, Plate, Light, Medium, Heavy, Wood, Steel, Concrete }
[Flags]public enum VeteranAbility { None=0,Firepower=1,Faster=2,Stronger=4,RateOfFire=8,SelfHeal=16 }
public sealed class WeaponProfile {
 public readonly string RuleId;public readonly int Damage,Burst,Frames;public readonly float Range,MinimumRange,Reload,Spread;public readonly float[] Verses;
 public bool BlockedByWalls => RuleId is "M60" or "M60E" or "Vulcan" or "Para" or "ParaE" or "UCPara" or "UCElitePara" or "AWP" or "AWPE" or "CRAWP" or "CRM60" or "105mm" or "105mmE";
 public bool DamagesWalls => RuleId is "105mm" or "105mmE" or "HowitzerGun" or "HoverMissile" or "HoverMissileE" or "MissileLauncher" or "MissileLauncherE" or "CRMissileLauncher";
 public WeaponProfile(string id,int damage,int frames,float cells,float minimum,int burst,float spread,float[] verses){RuleId=id;Damage=damage;Frames=frames;Range=cells*Campaign.Tile;MinimumRange=minimum*Campaign.Tile;Reload=frames/ReferenceBalance.FramesPerGameSecond;Burst=burst;Spread=spread*Campaign.Tile;Verses=verses;}
}
// Fixed CnCNet YR rules, e4b5712df96ec3acb05ddbda9fe2ba63e30576ab.
// Numeric source extraction and intentional archetype adaptations are documented
// in docs/research/战斗数值统一-20261109.md. No original engine code is copied.
public static class ReferenceBalance {
 public const float FramesPerGameSecond=15f, LeptonsPerCell=256f;
 public const float BuildSecondsPerCredit=.042f, BurstSpacing=3/FramesPerGameSecond;
 public const double VeteranRatio=3, VeteranCap=2;
 public const float VeteranCombat=1.1f,VeteranSpeed=1.2f,VeteranArmor=1.5f,VeteranROF=.6f;
 // SELF_HEAL uses RepairRate=.016 minutes, truncated to 14 simulation frames.
 // Hospital/machine-shop SelfHeal*Amount/Frames are a separate mechanic.
 public const int SelfHealFrames=14,SelfHealAmount=1;
 public const int OreValue=25, MinerStorage=40, StartingCredits=5750, TutorialCredits=8500;
 // Scenario-specific income cadence: not an asserted retail engine harvest rate.
 public const float HarvestCreditsPerSecond=180f, UnloadSeconds=1.6f;
 public static float MoveSpeed(int rating){return Math.Min(255,(int)(Math.Min(100,rating)*256/100f))*FramesPerGameSecond/LeptonsPerCell*Campaign.Tile;}
 public static PointF[] Circle(PointF center,float radius,int segments=128){
  var points=new PointF[segments];for(int i=0;i<segments;i++){double a=i*Math.PI*2/segments;points[i]=new PointF(center.X+(float)Math.Cos(a)*radius,center.Y+(float)Math.Sin(a)*radius);}return points;
 }
 internal static readonly WeaponProfile WM60=new WeaponProfile("M60",15,20,4.0f,0.0f,1,0.0f,new float[]{1.0f,0.8f,0.8f,0.5f,0.25f,0.25f,0.75f,0.5f,0.25f});
 internal static readonly WeaponProfile WM60E=new WeaponProfile("M60E",25,20,4.0f,0.0f,1,0.0f,new float[]{1.0f,0.8f,0.8f,0.5f,0.25f,0.25f,0.75f,0.5f,0.25f});
 internal static readonly WeaponProfile WMissileLauncher=new WeaponProfile("MissileLauncher",40,40,8.0f,1.0f,1,0.5f,new float[]{0.2f,0.2f,0.2f,1.0f,0.5f,1.0f,0.1f,0.1f,0.1f});
 internal static readonly WeaponProfile WMissileLauncherE=new WeaponProfile("MissileLauncherE",50,20,8.0f,1.0f,1,0.5f,new float[]{0.2f,0.2f,0.2f,1.0f,0.5f,1.0f,0.1f,0.1f,0.1f});
 internal static readonly WeaponProfile WHoverMissile=new WeaponProfile("HoverMissile",25,50,6.0f,1.0f,2,0.5f,new float[]{1.0f,1.0f,1.0f,0.7f,0.7f,0.35f,0.75f,0.4f,0.2f});
 internal static readonly WeaponProfile WHoverMissileE=new WeaponProfile("HoverMissileE",80,50,6.0f,1.0f,4,0.5f,new float[]{1.0f,1.0f,1.0f,0.7f,0.7f,0.35f,0.75f,0.4f,0.2f});
 internal static readonly WeaponProfile WTank105mm=new WeaponProfile("105mm",65,60,5.0f,0.0f,1,0.3f,new float[]{0.25f,0.25f,0.15f,0.75f,1.0f,1.0f,0.65f,0.45f,0.6f});
 internal static readonly WeaponProfile WTank105mmE=new WeaponProfile("105mmE",65,50,5.0f,0.0f,2,0.3f,new float[]{1.0f,1.0f,1.0f,1.0f,1.0f,1.0f,0.65f,0.45f,0.6f});
 internal static readonly WeaponProfile WHowitzerGun=new WeaponProfile("HowitzerGun",75,100,12.0f,2.0f,1,0.5f,new float[]{1.0f,0.9f,0.8f,0.6f,0.4f,0.4f,0.5f,0.4f,0.25f});
 internal static readonly WeaponProfile WVulcan=new WeaponProfile("Vulcan",50,26,5.5f,0.0f,1,0.0f,new float[]{1.0f,0.8f,0.8f,0.5f,0.25f,0.25f,0.75f,0.5f,0.25f});
 internal static readonly WeaponProfile WUCPara=new WeaponProfile("UCPara",30,15,6.0f,0.0f,1,0.0f,new float[]{1.0f,0.8f,0.8f,0.5f,0.25f,0.25f,0.75f,0.3f,0.2f});
 internal static readonly WeaponProfile WUCElitePara=new WeaponProfile("UCElitePara",40,15,6.0f,0.0f,1,0.0f,new float[]{1.0f,0.8f,0.8f,0.5f,0.25f,0.25f,0.75f,0.3f,0.2f});
 public static void Configure(TypeInfo t){
  switch(t.Key){
   case "hq": Set(t,"GACNST",3000,1000,ArmorKind.Concrete,0,8,0,null,null,false);break;
   case "power": Set(t,"GAPOWR",800,750,ArmorKind.Wood,0,4,200,null,null,false);break;
   case "barracks": Set(t,"GAPILE",500,500,ArmorKind.Steel,0,5,-10,null,null,false);break;
   case "factory": Set(t,"GAWEAP",2000,1000,ArmorKind.Wood,0,4,-25,null,null,false);break;
   case "radar": Set(t,"GAAIRC",1000,600,ArmorKind.Steel,0,5,-50,null,null,false);break;
   case "satellite": Set(t,"GASPYSAT",1500,1000,ArmorKind.Wood,0,5,-100,null,null,false);break;
   case "sniper": Set(t,"SNIPE",600,125,ArmorKind.None,4,8,0,WAWP,WAWPE,false);break;
   case "bunker": Set(t,"NABNKR",500,600,ArmorKind.Steel,0,6,0,null,null,false);break;
   case "wall": Set(t,"GAWALL",100,300,ArmorKind.Concrete,0,1,0,null,null,false);break;
   case "oilwell": Set(t,"CAOILD",0,1000,ArmorKind.Steel,0,6,0,null,null,false);break;
   case "post": Set(t,"NALASR",500,400,ArmorKind.Steel,0,7,0,WVulcan,WVulcan,false);break;
   case "squad": Set(t,"E1",200,125,ArmorKind.None,4,5,0,WM60,WM60E,true);break;
   case "at": Set(t,"GGI",400,100,ArmorKind.None,3,6,0,WM60,WM60E,true);break;
   case "engineer": Set(t,"ENGINEER",500,75,ArmorKind.None,4,4,0,null,null,false);break;
   case "ifv": Set(t,"FV",600,200,ArmorKind.Light,10,8,0,WHoverMissile,WHoverMissileE,false);break;
   case "tank": Set(t,"MTNK",700,300,ArmorKind.Heavy,7,8,0,WTank105mm,WTank105mmE,false);break;
   case "arty": Set(t,"HOWI",750,200,ArmorKind.Light,5,8,0,WHowitzerGun,WHowitzerGun,false);break;
   case "mcv": Set(t,"AMCV",3000,1000,ArmorKind.Heavy,4,6,0,null,null,false);break;
   case "refinery": Set(t,"GAREFN",2000,1000,ArmorKind.Wood,0,6,-50,null,null,false);break;
   case "miner": Set(t,"HARV",1400,1000,ArmorKind.Medium,4,4,0,null,null,false);break;
   default:throw new ArgumentException("Missing balance profile: "+t.Key);
  }
 }
 static void Set(TypeInfo t,string id,int price,int hp,ArmorKind armor,int speed,int sight,int energy,WeaponProfile weapon,WeaponProfile elite,bool veteranRof){
  t.RuleId=id;t.Price=price;t.Hp=hp;t.Armor=armor;t.SpeedRating=speed;t.Speed=MoveSpeed(speed);t.SightCells=sight;t.Energy=energy;
  t.Weapon=weapon;t.EliteWeapon=elite;t.Hit=weapon==null?0:weapon.Damage;t.Range=weapon==null?0:(int)weapon.Range;t.Reload=weapon==null?0:weapon.Reload;
  t.Trainable=speed>0&&t.Key!="engineer"&&t.Key!="mcv";t.SelfHealing=t.Key=="miner";
  if(t.Trainable){
   t.VeteranAbilities=VeteranAbility.Stronger|VeteranAbility.Firepower|VeteranAbility.Faster;
   if(veteranRof)t.VeteranAbilities|=VeteranAbility.RateOfFire;
   t.EliteAbilities=VeteranAbility.SelfHeal|VeteranAbility.Stronger|VeteranAbility.Firepower|VeteranAbility.RateOfFire;
  }
  t.Seconds=price*BuildSecondsPerCredit;
 }
 internal static readonly WeaponProfile WPara=new WeaponProfile("Para",25,15,5,0,1,0,new float[]{1,1,1,.6f,.4f,.4f,.75f,.5f,.25f});
 internal static readonly WeaponProfile WParaE=new WeaponProfile("ParaE",25,15,6,0,1,0,new float[]{1,1,1,.6f,.4f,.4f,.75f,.5f,.25f});
 internal static readonly WeaponProfile WAWP=new WeaponProfile("AWP",125,150,14,0,1,0,new float[]{2,1,1,.01f,.01f,.01f,.01f,.01f,.01f});
 internal static readonly WeaponProfile WAWPE=new WeaponProfile("AWPE",125,60,14,0,1,0,new float[]{2,1,1,.01f,.01f,.01f,.01f,.01f,.01f});
 internal static readonly WeaponProfile WCRAWP=new WeaponProfile("CRAWP",250,100,14,0,1,0,new float[]{1,1,1,0,0,0,.01f,.01f,.01f});
 internal static readonly WeaponProfile WCRM60=new WeaponProfile("CRM60",25,15,6,0,1,0,new float[]{1,1,1,.6f,.4f,.4f,.75f,.5f,.25f});
 internal static readonly WeaponProfile WCRMissile=new WeaponProfile("CRMissileLauncher",30,40,8,1,2,.5f,new float[]{.2f,.2f,.2f,1,.5f,1,.1f,.1f,.1f});
 internal static readonly WeaponProfile WRepair=new WeaponProfile("RepairBullet",-50,80,1.8f,0,1,0,new float[]{0,0,0,1,1,1,0,0,0});
 public static float RepairAmount(Piece car){return -WRepair.Damage*(HasAbility(car,VeteranAbility.Firepower)?VeteranCombat:1);}
 public static float Range(Piece p){var w=Weapon(p);return w==null?0:w.Range;}
 public static float LockRange(Piece p){float range=Range(p);return p.T.Building?range:Math.Min(range*2,16*Campaign.Tile);}
 public static string IFVRole(Piece car){var u=car.Passenger;return u==null||!u.Alive?"空车 · 导弹":u.T.Key=="engineer"?"工兵 · 维修车":u.T.Key=="squad"?"步枪兵 · 机枪":u.T.Key=="at"?"反装甲兵 · 反装甲导弹":u.T.Key=="sniper"?"狙击手 · 狙击炮塔":"导弹";}
 public static WeaponProfile Weapon(Piece p){
  if(p.T.Key=="ifv"&&p.Passenger!=null&&p.Passenger.Alive){
   switch(p.Passenger.T.Key){case "engineer":return null;case "squad":return WCRM60;case "at":return WCRMissile;case "sniper":return WCRAWP;}
  }
  if(p.Carrier!=null&&p.Carrier.T.Garrison&&p.T.Key=="squad")return p.Rank==2?WUCElitePara:WUCPara;
  if(p.T.Key=="at"&&p.Carrier!=null&&p.Carrier.T.Civilian)return p.Rank==2?WMissileLauncherE:WMissileLauncher;
  if(p.Carrier==null&&p.Entrenched){if(p.T.Key=="squad")return p.Rank==2?WParaE:WPara;if(p.T.Key=="at")return p.Rank==2?WMissileLauncherE:WMissileLauncher;}
  return p.Rank==2?p.T.EliteWeapon:p.T.Weapon;
 }
 public static bool HasAbility(Piece p,VeteranAbility ability){
  var active=p.Rank>0?p.T.VeteranAbilities:VeteranAbility.None;if(p.Rank==2)active|=p.T.EliteAbilities;
  return (active&ability)!=0;
 }
 public static float Reload(Piece p,WeaponProfile w){return w.Reload*(HasAbility(p,VeteranAbility.RateOfFire)?VeteranROF:1);}
 public static float Speed(Piece p){return p.T.Speed*(HasAbility(p,VeteranAbility.Faster)?VeteranSpeed:1);}
 public static float Damage(Piece source,Piece target,WeaponProfile weapon){
  float firepower=HasAbility(source,VeteranAbility.Firepower)?VeteranCombat:1;
  if(target.T.IsWall)return weapon.DamagesWalls?weapon.Damage*firepower:0;
  return weapon.Damage*weapon.Verses[(int)target.T.Armor]*firepower/(HasAbility(target,VeteranAbility.Stronger)?VeteranArmor:1);
 }
}
}
