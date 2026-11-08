using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 sealed class HotkeySpec {public string Id,Label;public int Default;public Keys Canonical;public HotkeySpec(string id,string label,int value,Keys key){Id=id;Label=label;Default=value;Canonical=key;}}
 readonly Dictionary<string,int> hotkeyValues=new Dictionary<string,int>();List<HotkeySpec> hotkeySpecs;bool hotkeysLoaded;string hotkeyPathOverride=null;Dictionary<string,int> keyboardBefore;
 string HotkeyFile {get{return hotkeyPathOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Greyhaven RTS","keyboard.ini");}}
 void EnsureHotkeys(){
  if(hotkeysLoaded)return;hotkeysLoaded=true;hotkeySpecs=new List<HotkeySpec>{
   new HotkeySpec("CenterView","定位所选单位",12,Keys.Clear),new HotkeySpec("Options","选项菜单",27,Keys.Escape),new HotkeySpec("CenterOnRadarEvent","定位雷达事件",32,Keys.Space),
   new HotkeySpec("ToggleAlliance","尝试结盟（本关锁定）",65,Keys.A),new HotkeySpec("PlaceBeacon","放置战场标记",66,Keys.B),new HotkeySpec("AllToCheer","步兵庆祝",67,Keys.C),
   new HotkeySpec("DeployObject","部署／收起／卸载",68,Keys.D),new HotkeySpec("InfantryTab","步兵分类",69,Keys.E),new HotkeySpec("Follow","切换跟随镜头",70,Keys.F),
   new HotkeySpec("GuardObject","原地警戒",71,Keys.G),new HotkeySpec("CenterBase","定位基地",72,Keys.H),new HotkeySpec("ToggleRepair","维修模式",75,Keys.K),
   new HotkeySpec("ToggleSell","出售模式",76,Keys.L),new HotkeySpec("PreviousObject","上一个单位",77,Keys.M),new HotkeySpec("NextObject","下一个单位",78,Keys.N),
   new HotkeySpec("CombatantSelect","全选战斗单位",80,Keys.P),new HotkeySpec("StructureTab","建筑分类",81,Keys.Q),new HotkeySpec("UnitTab","车辆分类",82,Keys.R),
   new HotkeySpec("StopObject","停止",83,Keys.S),new HotkeySpec("TypeSelect","同类选择",84,Keys.T),new HotkeySpec("PageUser","私聊（单机不适用）",85,Keys.U),
   new HotkeySpec("DefenseTab","防御分类",87,Keys.W),new HotkeySpec("ScatterObject","散开",88,Keys.X),new HotkeySpec("VeterancyNav","按等级筛选",89,Keys.Y),
   new HotkeySpec("PlanningMode","路径点规划",90,Keys.Z),new HotkeySpec("SidebarDown","生产列表向下",98,Keys.NumPad2),new HotkeySpec("SidebarUp","生产列表向上",104,Keys.NumPad8),
   new HotkeySpec("Delete","删除标记／路径点",110,Keys.Delete),new HotkeySpec("ScreenCapture","保存截图",339,Keys.Shift|Keys.S),new HotkeySpec("Diplomacy","阵营信息",9,Keys.Tab),
   new HotkeySpec("HealthNav","按血量筛选（未分配）",0,Keys.F13)
  };
  for(int i=0;i<4;i++){hotkeySpecs.Add(new HotkeySpec("View"+(i+1),"调用镜头 "+(i+1),112+i,(Keys)((int)Keys.F1+i)));hotkeySpecs.Add(new HotkeySpec("SetView"+(i+1),"记录镜头 "+(i+1),624+i,Keys.Control|(Keys)((int)Keys.F1+i)));}
  for(int i=1;i<=10;i++){int digit=i==10?0:i;Keys key=(Keys)((int)Keys.D0+digit);
   hotkeySpecs.Add(new HotkeySpec("TeamSelect_"+i,"选择编组 "+digit,48+digit,key));hotkeySpecs.Add(new HotkeySpec("TeamCreate_"+i,"建立编组 "+digit,560+digit,Keys.Control|key));
   hotkeySpecs.Add(new HotkeySpec("TeamAddSelect_"+i,"加选编组 "+digit,304+digit,Keys.Shift|key));hotkeySpecs.Add(new HotkeySpec("TeamCenter_"+i,"定位编组 "+digit,1072+digit,Keys.Alt|key));
  }
  for(int i=0;i<8;i++)hotkeySpecs.Add(new HotkeySpec("Taunt_"+(i+1),"语音嘲讽（单机不适用）"+(i+1),116+i,(Keys)((int)Keys.F5+i)));
  DefaultHotkeys();
  try{if(File.Exists(HotkeyFile))foreach(string line in File.ReadAllLines(HotkeyFile)){var entry=line.Split('=');int code;if(entry.Length==2&&hotkeyValues.ContainsKey(entry[0].Trim())&&int.TryParse(entry[1].Trim(),out code)&&code>=0&&code<2048)hotkeyValues[entry[0].Trim()]=code;}}
  catch(IOException){keyboardError="无法读取按键设置，已使用默认键位。";}catch(UnauthorizedAccessException){keyboardError="无法读取按键设置，已使用默认键位。";}
 }
 void DefaultHotkeys(){foreach(var spec in hotkeySpecs)hotkeyValues[spec.Id]=spec.Default;}
 static int EncodeKey(KeyEventArgs e){int code=e.KeyValue;if(code==(int)Keys.NumPad5)code=12;if(code==(int)Keys.Delete)code=110;return code+(e.Shift?256:0)+(e.Control?512:0)+(e.Alt?1024:0);}
 bool MapHotkey(KeyEventArgs raw,out KeyEventArgs mapped){
  EnsureHotkeys();int code=EncodeKey(raw);var spec=hotkeySpecs.FirstOrDefault(s=>hotkeyValues[s.Id]!=0&&hotkeyValues[s.Id]==code);mapped=spec==null?null:new KeyEventArgs(spec.Canonical);
  if(spec!=null&&spec.Id=="PlanningMode")planningPhysicalKey=raw.KeyCode;if(spec!=null&&spec.Id=="TypeSelect")typePhysicalKey=raw.KeyCode;return spec!=null;
 }
 string Shortcut(string id){EnsureHotkeys();return " ["+KeyLabel(hotkeyValues[id])+"]";}
 string KeyLabel(int code){if(code==0)return "未分配";var parts=new List<string>();if((code&512)!=0)parts.Add("Ctrl");if((code&256)!=0)parts.Add("Shift");if((code&1024)!=0)parts.Add("Alt");int key=code&255;parts.Add(key==12?"数字键盘5":key==110?"Delete":key>=48&&key<=57?(key-48).ToString():((Keys)key).ToString());return String.Join(" + ",parts);}
 void OpenKeyboard(){EnsureHotkeys();keyboardBefore=new Dictionary<string,int>(hotkeyValues);paused=true;help=false;keyboardOpen=true;keyboardCapturing=false;keyboardPage=0;diplomacyOpen=false;rightPending=rightDragging=false;Capture=false;}
 bool CaptureKeyboard(KeyEventArgs e){
  if(!keyboardOpen)return false;if(!keyboardCapturing){if(e.KeyCode==Keys.Escape)CancelKeyboard();return true;}
  if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return true;
  if(e.KeyCode==Keys.Escape){keyboardCapturing=false;return true;}
  int code=e.KeyCode==Keys.Back?0:EncodeKey(e);var conflict=hotkeySpecs.FirstOrDefault(s=>s.Id!=keyboardCommand&&code!=0&&hotkeyValues[s.Id]==code);
  if(conflict!=null)hotkeyValues[conflict.Id]=0;
  hotkeyValues[keyboardCommand]=code;keyboardCapturing=false;keyboardError=conflict==null?"":"原「"+conflict.Label+"」绑定已清除；取消可恢复。";return true;
 }
 void CancelKeyboard(){if(keyboardBefore!=null){hotkeyValues.Clear();foreach(var item in keyboardBefore)hotkeyValues[item.Key]=item.Value;}keyboardOpen=false;keyboardCapturing=false;keyboardError="";}
 void SaveHotkeys(){
  try{Directory.CreateDirectory(Path.GetDirectoryName(HotkeyFile));var text=new StringBuilder("[Hotkey]\r\n");foreach(var s in hotkeySpecs)text.Append(s.Id).Append('=').Append(hotkeyValues[s.Id]).Append("\r\n");File.WriteAllText(HotkeyFile,text.ToString(),new UTF8Encoding(false));keyboardOpen=false;keyboardCapturing=false;keyboardError="";}
  catch(IOException){keyboardError="无法保存按键设置，请检查目录。";}catch(UnauthorizedAccessException){keyboardError="保存目录不能写入。";}
 }
 void DrawKeyboard(Graphics g){
  Plate(g,new RectangleF(235,65,1000,690),panel,gold);Txt(g,"键盘与鼠标",266,88,29,pale,true);Txt(g,"点击按键行后按新组合键；Backspace 清除，Esc 取消输入。",267,130,15,dim);
  int pages=(hotkeySpecs.Count+10)/11;keyboardPage=Math.Max(0,Math.Min(pages-1,keyboardPage));
  for(int row=0;row<11;row++){int index=keyboardPage*11+row;if(index>=hotkeySpecs.Count)break;var spec=hotkeySpecs[index];float y=166+row*40;
   Txt(g,spec.Label,268,y+7,15,pale);Button(g,760,y,430,33,keyboardCapturing&&keyboardCommand==spec.Id?"请按键…":KeyLabel(hotkeyValues[spec.Id]),()=>{keyboardCommand=spec.Id;keyboardCapturing=true;keyboardError="";});hits.Last().Id="bind-"+spec.Id;
  }
  Txt(g,keyboardError.Length>0?keyboardError:"鼠标：左键选择／指挥；右键单击取消、拖动滚屏。",268,616,14,keyboardError.Length>0?red:dim);
  Button(g,268,654,65,34,"‹",()=>keyboardPage=Math.Max(0,keyboardPage-1));Txt(g,(keyboardPage+1)+" / "+pages,350,663,15,gold,true);Button(g,421,654,65,34,"›",()=>keyboardPage=Math.Min(pages-1,keyboardPage+1));
  Button(g,530,654,180,34,"恢复默认",()=>{DefaultHotkeys();keyboardError="";});Button(g,738,654,180,34,"应用并返回",SaveHotkeys,true);Button(g,944,654,180,34,"返回选项",CancelKeyboard);
 }
}
}
