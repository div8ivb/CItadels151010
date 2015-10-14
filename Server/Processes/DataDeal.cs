﻿using Server.Datas;
using Server.Net;
using Server.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Processes
{
   public static class DataDeal
    {
        //预处理，去掉‘*’的影响，以免数据相连造成异常
        public static void DealDataPre(DataCenter dataCenter, Socket socket, string str)
        {

            string[] strs = str.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in strs)
            {
                DealData(dataCenter, socket, item);
            }
        }

        public static void DealData(DataCenter dataCenter,Socket socket,string str)
        {
            string[] strs = str.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            //此处处理客户端传过来的信息
            //1信息。2大厅。
            switch (strs[0])
            {
                case "1":
                    //1连接请求。2注册信息。3登录信息。
                    PlayerData playerData = InfoDeal.InfoDataDeal(App.vM.SQLCtrl, socket, strs);
                    if (playerData.Status.Contains("1|1|1|"))
                    { break; }
                    if (playerData.Status.Contains("1|3|1|"))
                    { SignDeal(dataCenter, socket, playerData); }
                    NetCtrl.Send(socket, playerData.Status);
                    if (playerData.Status.Contains("1|3|1|"))
                    { playerData.Status = "Online"; dataCenter.LobbyPlayerList.Add(playerData); }
                    break;
                case "2":
                    DealLobbyData(dataCenter,socket,strs);break;

            }
        }
        #region 通用函数
        //给特定房间的人发送数据
        private static void SendToRoom(DataCenter dataCenter, int rNum, string str)
        {
            foreach (var item in dataCenter.RoomDataDic[rNum].PlayerDataList)
            {
                NetCtrl.Send(item.Socket, str);
            }
        }
        #endregion


        #region 处理信息信息(strs[0]="1")
        private static void SignDeal(DataCenter dataCenter, Socket socket, PlayerData playerData)
        {
            foreach (var item in dataCenter.LobbyPlayerList)
            {
                if (item.Mail == playerData.Mail)
                {
                    switch(item.Status)
                    {
                        case "Online":playerData.Status = "1|3|-1|您的账号已在大厅了|";return;
                        case "Break":item.Socket = socket;item.Status = "Online";return;
                    }
                }
            }
            foreach (var room in dataCenter.RoomDataDic)
            {
                foreach (var player in room.Value.PlayerDataList)
                {
                    if(player.Mail==playerData.Mail)
                    {
                        switch (player.Status)
                        {
                            case "Online": playerData.Status = "1|3|-1|您的账号已经在游戏了|"; return;
                            case "Break": player.Socket = socket; player.Status = "Online";  return;
                        }
                    }
                }
            }        
        }
        #endregion

        #region 处理大厅信息(strs[0]="2")
        private static void DealLobbyData(DataCenter dataCenter, Socket socket, string[] strs)
        {
            switch(strs[1])
            {
                //处理创建房间数据
                case "1":
                    DealLobbyData1(dataCenter,socket,strs);
                    break;
                //处理加入房间数据
                case "2":
                    DealLobbyData2(dataCenter, socket, strs);
                    break;
                    //处理请求信息数据
                case "3":
                    DealLobbyData3(dataCenter,socket,strs);
                    break;
                    //处理开始游戏数据
                case "4":
                    DealLobbyData4(dataCenter, socket, strs);
                    break;

            }
        }

        private static void DealLobbyData4(DataCenter dataCenter, Socket socket, string[] strs)
        {
            int rNum = int.Parse(strs[2]);
            dataCenter.RoomDataDic[rNum].Status = "已开始";
            SendRoughLobbyData(dataCenter);
            SendToRoom(dataCenter, rNum, "2|4|1|");
            for (int i = 0; i < dataCenter.RoomDataDic[rNum].PlayerDataList.Count; i++)
            {
                int index = dataCenter.LobbyPlayerList.FindIndex(s => s.RNum == rNum);
                dataCenter.LobbyPlayerList.RemoveAt(index);
            }
            SendToRoom(dataCenter, rNum, "2|4|1|");
        }


        //处理信息请求的数据
        private static void DealLobbyData3(DataCenter dataCenter, Socket socket, string[] strs)
        {
            switch(strs[2])
            {
                //请求粗略信息
                case "1":
                    string str = "2|3|1|";
                    foreach (var item in dataCenter.RoomDataDic)
                    {
                        str += item.Key;
                        str += "|";
                        str += item.Value.PlayerDataList.Count;
                        str += "|";
                        str += item.Value.PlayerDataList[0].Nick;
                        str += "|";
                        str += item.Value.Status;
                        str += "|";
                    }
                    NetCtrl.Send(socket, str);
                    break;
            }
        }

        //处理大厅的加入房间数据
        private static void DealLobbyData2(DataCenter dataCenter, Socket socket, string[] strs)
        {
            int rNum = int.Parse(strs[2]);
            int index = dataCenter.LobbyPlayerList.FindIndex(i => i.Mail == strs[3]);
            dataCenter.LobbyPlayerList[index].RNum = rNum;
            dataCenter.LobbyPlayerList[index].SNum = dataCenter.RoomDataDic[rNum].PlayerDataList.Count + 1;
            dataCenter.RoomDataDic[rNum].PlayerDataList.Add(dataCenter.LobbyPlayerList[index]);
            NetCtrl.Send(socket,"2|2|1|"+rNum+"|"+dataCenter.RoomDataDic[rNum].PlayerDataList.Count+"|");
            NetCtrl.Send(dataCenter.RoomDataDic[rNum].PlayerDataList[0].Socket, "2|4|2");
            SendDetailLobbyData(dataCenter,rNum);
            SendRoughLobbyData(dataCenter);
        }

        //处理大厅创建房间的数据
        private static void DealLobbyData1(DataCenter dataCenter, Socket socket, string[] strs)
        {
            dataCenter.RoomNum = 1;
            while(dataCenter.RoomDataDic.Keys.Contains(dataCenter.RoomNum))
            { dataCenter.RoomNum++; }
            int index = dataCenter.LobbyPlayerList.FindIndex(i => i.Mail == strs[2]);
            RoomData rd = new RoomData();
            dataCenter.LobbyPlayerList[index].IsKill = true;
            dataCenter.LobbyPlayerList[index].RNum = dataCenter.RoomNum;
            dataCenter.LobbyPlayerList[index].SNum = 1;
            rd.PlayerDataList.Add(dataCenter.LobbyPlayerList[index]);
            dataCenter.RoomDataDic.Add(dataCenter.RoomNum, rd);
            NetCtrl.Send(socket, "2|1|1|" + dataCenter.RoomNum + "|" + "1|");
            SendDetailLobbyData(dataCenter, dataCenter.RoomNum);
            SendRoughLobbyData(dataCenter);
        }

        //给在大厅中的人发送粗略信息数据
        private static void SendRoughLobbyData(DataCenter dataCenter)
        {
            string str = "2|3|1|";
            foreach (var item in dataCenter.RoomDataDic)
            {
                str += item.Key;
                str += "|";
                str += item.Value.PlayerDataList.Count;
                str += "|";
                str += item.Value.PlayerDataList[0].Nick;
                str += "|";
                str += item.Value.Status;
                str += "|";
            }
            foreach (var item in dataCenter.LobbyPlayerList)
            {
                NetCtrl.Send(item.Socket, str);
            }
        }

        //给在特定房间内的人发送详细信息
        private static void SendDetailLobbyData(DataCenter dataCenter, int rNum)
        {
            string str = "2|3|2|"+rNum+"|";
            foreach (var item in dataCenter.RoomDataDic[rNum].PlayerDataList)
            {
                str += item.SNum;
                str += "|";
                str += item.Nick;
                str += "|";
                str += item.Exp;
                str += "|";
            }
            foreach (var item in dataCenter.RoomDataDic[rNum].PlayerDataList)
            {
                NetCtrl.Send(item.Socket, str);
            }
        }
        
        #endregion
    }
}