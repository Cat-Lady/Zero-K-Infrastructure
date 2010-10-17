﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LobbyClient;
using PlasmaShared;
using ZeroKLobby.Lines;

namespace ZeroKLobby.MicroLobby
{
	class BattleChatControl: ChatControl
	{
		Image minimap;
		PictureBox minimapBox;
		Size minimapSize;
		public static event EventHandler<EventArgs<IChatLine>> BattleLine = delegate { };

		public BattleChatControl(): base("Battle") {}

		protected override void Dispose(bool disposing)
		{
			if (Program.TasClient != null) Program.TasClient.UnsubscribeEvents(this);
			if (Program.QuickMatchTracker != null) Program.QuickMatchTracker.UnsubscribeEvents(this);
			base.Dispose(disposing);
		}

		public override void AddLine(IChatLine line)
		{
			base.AddLine(line);
			BattleLine(this, new EventArgs<IChatLine>(line));
		}


		protected override void OnLoad(EventArgs ea)
		{
			base.OnLoad(ea);
			Program.TasClient.Said += TasClient_Said;
			Program.TasClient.BattleJoined += TasClient_BattleJoined;
			Program.TasClient.BattleUserLeft += TasClient_BattleUserLeft;
			Program.TasClient.BattleUserJoined += TasClient_BattleUserJoined;
			Program.TasClient.BattleUserStatusChanged += TasClient_BattleUserStatusChanged;
			Program.TasClient.BattleClosed += (s, e) => Reset();
			Program.TasClient.ConnectionLost += (s, e) => Reset();
			Program.TasClient.BattleBotAdded += (s, e) => SortByTeam();
			Program.TasClient.BattleBotRemoved += (s, e) => SortByTeam();
			Program.TasClient.BattleBotUpdated += (s, e) => SortByTeam();
			Program.TasClient.BattleMapChanged += TasClient_BattleMapChanged;
			Program.TasClient.StartRectAdded += (s, e) => DrawMinimap();
			Program.TasClient.StartRectRemoved += (s, e) => DrawMinimap();
			Program.QuickMatchTracker.PlayerQuickMatchChanged += (s, e) => RefreshBattleUser(e.Data);

			if (Program.TasClient.MyBattle != null) foreach (var user in Program.TasClient.MyBattle.Users) AddUser(user.Name);
			ChatLine += (s, e) => { if (Program.TasClient.IsLoggedIn) Program.TasClient.Say(TasClient.SayPlace.Battle, null, e.Data, false); };
			playerBox.IsBattle = true;
			playerBox.MouseDown += playerBox_MouseDown;

			minimapBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage };
			minimapBox.Cursor = Cursors.Hand;
			minimapBox.Click +=
				(s, e) => { if (Program.TasClient.MyBattle != null) Utils.OpenWeb(string.Format("http://planet-wars.eu/PlasmaServer/ResourceDetail.aspx?name={0}", Program.TasClient.MyBattle.MapName)); };

			mapPanel.Controls.Add(minimapBox);
			mapPanel.Visible = true;
			mapPanel.Height = playerBox.Width;
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			mapPanel.Height = playerBox.Width;
			DrawMinimap();
		}

		public override void Reset()
		{
			base.Reset();
			minimapBox.Image = null;
			minimap = null;
			Program.ToolTip.Clear(minimapBox);
		}

		protected override void SortByTeam()
		{
			if (filtering || Program.TasClient.MyBattle == null) return;

			var newList = new List<PlayerListItem>();
			foreach (var us in PlayerListItems) newList.Add(us);

			var nonSpecs = PlayerListItems.Where(p => p.UserBattleStatus != null && !p.UserBattleStatus.IsSpectator);
			var existingTeams = nonSpecs.GroupBy(i => i.UserBattleStatus.AllyNumber).Select(team => team.Key).ToList();

			foreach (var bot in Program.TasClient.MyBattle.Bots)
			{
				newList.Add(new PlayerListItem { BotBattleStatus = bot, SortCategory = bot.AllyNumber*2 + 1, AllyTeam = bot.AllyNumber });
				existingTeams.Add(bot.AllyNumber);
			}

			// add section headers
			if (PlayerListItems.Any(i => i.UserBattleStatus != null && i.UserBattleStatus.IsSpectator)) newList.Add(new PlayerListItem { Button = "Spectators", SortCategory = 100, IsSpectatorsTitle = true, Height = 25 });
			foreach (var team in existingTeams.Distinct()) newList.Add(new PlayerListItem { Button = "Team " + (team + 1), SortCategory = team*2, AllyTeam = team, Height = 25 });

			newList = newList.OrderBy(x => x.ToString()).ToList();

			playerBox.Items.Clear();
			playerBox.BeginUpdate();
			
			foreach (var item in newList) playerBox.Items.Add(item);
			/*
			var oldIndex = 0;
			var newIndex = 0;
			while (oldIndex < playerBox.Items.Count || newIndex < newList.Count)
			{
				if (oldIndex >= playerBox.Items.Count)
				{
					playerBox.Items.Add(newList[newIndex]);
					newIndex++;
				}
				else if (newIndex >= newList.Count) playerBox.Items.RemoveAt(oldIndex);
				else
				{
					var oldItem = (PlayerListItem)playerBox.Items[oldIndex];
					var newItem = newList[newIndex];
					int compVal = string.Compare(oldItem.ToString(), newItem.ToString());
					if (compVal < 0)
					{
						playerBox.Items.RemoveAt(oldIndex);
						playerBox.Items.Insert(oldIndex, newItem);
					}
					else if (compVal > 0) playerBox.Items.Insert(oldIndex, newItem);
					oldIndex++;
					newIndex++;
				}
			}*/
			playerBox.EndUpdate();
		}

		protected override void client_ChannelUserAdded(object sender, TasEventArgs e) {}

		protected override void client_ChannelUserRemoved(object sender, TasEventArgs e) {}

		void DrawMinimap()
		{
			if (minimap == null) return;
			var boxColors = new[]
			                {
			                	Color.Green, Color.Red, Color.Blue, Color.Cyan, Color.Yellow, Color.Magenta, Color.Gray, Color.Lime, Color.Maroon, Color.Navy,
			                	Color.Olive, Color.Purple, Color.Silver, Color.Teal, Color.White,
			                };
			var xScale = (double)minimapBox.Width/minimapSize.Width; // todo remove minimapSize and use minimap image directly when plasmaserver stuff fixed
			var yScale = (double)minimapBox.Height/minimapSize.Height;
			var scale = Math.Min(xScale, yScale);
			minimapBox.Image = minimap.GetResized((int)(scale*minimapSize.Width), (int)(scale*minimapSize.Height), InterpolationMode.HighQualityBicubic);
			using (var g = Graphics.FromImage(minimapBox.Image))
			{
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				foreach (var kvp in Program.TasClient.MyBattle.Rectangles)
				{
					var startRect = kvp.Value;
					var allyTeam = kvp.Key;
					var left = startRect.Left*minimapBox.Image.Width/BattleRect.Max;
					var top = startRect.Top*minimapBox.Image.Height/BattleRect.Max;
					var right = startRect.Right*minimapBox.Image.Width/BattleRect.Max;
					var bottom = startRect.Bottom*minimapBox.Image.Height/BattleRect.Max;
					var width = right - left;
					var height = bottom - top;
					if (width < 1 || height < 1) continue;
					var drawRect = new Rectangle(left, top, width, height);
					var color = allyTeam < boxColors.Length
					            	? Color.FromArgb(255/2, boxColors[allyTeam].R, boxColors[allyTeam].G, boxColors[allyTeam].B)
					            	: Color.Black;
					using (var brush = new SolidBrush(color)) g.FillRectangle(brush, drawRect);
					var middleX = left + width/2;
					var middleY = top + height/2;
					const int numberSize = 40;
					var numberRect = new Rectangle(middleX - numberSize/2, middleY - numberSize/2, numberSize, numberSize);
					using (var format = new StringFormat())
					{
						format.Alignment = StringAlignment.Center;
						format.LineAlignment = StringAlignment.Center;

						using (var font = new Font("Arial", 13f, FontStyle.Bold)) g.DrawStringWithOutline((allyTeam + 1).ToString(), font, Brushes.White, Brushes.Black, numberRect, format, 5);
					}
				}
			}
			minimapBox.Invalidate();
			// todo: drawing start points goes here, once we have the metadata
		}

		void RefreshBattleUser(string userName)
		{
			if (Program.TasClient.MyBattle == null) return;
			var userBattleStatus = Program.TasClient.MyBattle.Users.SingleOrDefault(u => u.Name == userName);
			if (userBattleStatus != null)
			{
				AddUser(userName);
				SortByTeam();
			}
		}

		void SetMapImages(string mapName)
		{
			Program.ToolTip.SetMap(minimapBox, mapName);

			// todo add check before calling invoke invokes!!!
			Program.SpringScanner.MetaData.GetMapAsync(mapName,
			                                           (map, minimap, heightmap, metalmap) => Invoke(new Action(() =>
			                                           	{
			                                           		if (Program.TasClient.MyBattle == null) return;
			                                           		if (map != null && map.Name != Program.TasClient.MyBattle.MapName) return;
			                                           		if (minimap == null || minimap.Length == 0)
			                                           		{
			                                           			minimapBox.Image = null;
			                                           			this.minimap = null;
			                                           		}
			                                           		else
			                                           		{
			                                           			this.minimap = Image.FromStream(new MemoryStream(minimap));
			                                           			minimapSize = map.Size;
			                                           			DrawMinimap();
			                                           		}
			                                           	})),
			                                           a => Invoke(new Action(() =>
			                                           	{
			                                           		minimapBox.Image = null;
			                                           		minimap = null;
			                                           	})));
		}


		void TasClient_BattleJoined(object sender, EventArgs<Battle> e)
		{
			Reset();
			SetMapImages(e.Data.MapName);
			foreach (var user in Program.TasClient.MyBattle.Users) AddUser(user.Name);
		}

		void TasClient_BattleMapChanged(object sender, BattleInfoEventArgs e1)
		{
			var battleID = e1.BattleID;
			if (Program.TasClient.MyBattle == null || battleID != Program.TasClient.MyBattle.BattleID) return;
			var mapName = e1.MapName;
			SetMapImages(mapName);
		}

		void TasClient_BattleUserJoined(object sender, BattleUserEventArgs e1)
		{
			var battleID = e1.BattleID;
			if (Program.TasClient.MyBattle != null && battleID == Program.TasClient.MyBattle.BattleID)
			{
				var userName = e1.UserName;
				var userBattleStatus = Program.TasClient.MyBattle.Users.Single(u => u.Name == userName);
				AddUser(userBattleStatus.Name);
				AddLine(new JoinLine(userName));
			}
		}

		void TasClient_BattleUserLeft(object sender, BattleUserEventArgs e)
		{
			var userName = e.UserName;
			if (userName == Program.Conf.LobbyPlayerName)
			{
				playerListItems.Clear();
				playerBox.Items.Clear();
			}
			if (PlayerListItems.Any(i => i.UserName == userName))
			{
				RemoveUser(userName);
				AddLine(new LeaveLine(userName));
			}
		}

		void TasClient_BattleUserStatusChanged(object sender, TasEventArgs e)
		{
			var userName = e.ServerParams[0];
			RefreshBattleUser(userName);
		}


		void TasClient_Said(object sender, TasSayEventArgs e)
		{
			if (e.Place == TasSayEventArgs.Places.Battle && e.Origin == TasSayEventArgs.Origins.Player)
			{
				if (e.Text.Contains(Program.Conf.LobbyPlayerName) && !Program.TasClient.MyUser.IsInGame && !e.IsEmote && !e.Text.StartsWith(string.Format("[{0}]", Program.TasClient.UserName )))
				{
					if (FormMain.Instance.ChatTab.Flash("Battle")) 
					FormMain.Instance.NotifyUser(string.Format("{0}: {1}", e.UserName, e.Text), false, true);
				}
				if (!e.IsEmote) AddLine(new SaidLine(e.UserName, e.Text));
				else AddLine(new SaidExLine(e.UserName, e.Text));
			}
		}

		void playerBox_MouseDown(object sender, MouseEventArgs mea)
		{
			switch (mea.Button)
			{
				case MouseButtons.Left:
					if (playerBox.HoverItem != null)
					{
						if (playerBox.HoverItem.IsSpectatorsTitle) ActionHandler.Spectate();
						else if (playerBox.HoverItem.AllyTeam.HasValue) ActionHandler.JoinAllyTeam(playerBox.HoverItem.AllyTeam.Value);
					}
					break;
				case MouseButtons.Right:
					if (playerBox.HoverItem == null)
					{
						var cm = ContextMenus.GetPlayerContextMenu(Program.TasClient.MyUser, true);
						Program.ToolTip.Visible = false;
						cm.Show(playerBox, mea.Location);
						Program.ToolTip.Visible = true;
					}
					else if (playerBox.HoverItem.BotBattleStatus != null)
					{
						playerBox.SelectedItem = playerBox.HoverItem;
						var cm = ContextMenus.GetBotContextMenu(playerBox.HoverItem.BotBattleStatus.Name);
						Program.ToolTip.Visible = false;
						cm.Show(playerBox, mea.Location);
						Program.ToolTip.Visible = true;
					}
					break;
			}
		}
	}
}