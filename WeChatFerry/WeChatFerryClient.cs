using Google.Protobuf;
using nng;
using Roomdata;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wcf;
using WeChatFerry.Abstractions.Model;

namespace WeChatFerry
{
  public class WeChatFerryClient : IDisposable
  {
    private readonly string msgUrl;
    private readonly IAPIFactory<INngMsg> Factory;

    public IPairSocket CmdSocket { get; private set; }
    public IPairSocket MsgSocket { get; private set; }

    public WeChatFerryClient(int port)
    {
      var url = "tcp://127.0.0.1";
      msgUrl = url + ":" + (port + 1);
      var managedAssemblyPath = Path.GetDirectoryName(typeof(WeChatFerryServer).Assembly.Location);
      var alc = new NngLoadContext(managedAssemblyPath);
      Factory = NngLoadContext.Init(alc, "nng.Factories.Latest.Factory");
      CmdSocket = Factory.PairOpen().ThenDial(url + ":" + port).Unwrap();
    }

    /// <summary>
    /// 检查连接是否有效
    /// </summary>
    /// <returns></returns>
    public bool IsValid()
    {
      return CmdSocket.IsValid();
    }

    // 检查登录状态
    // return bool 是否已登录
    public bool IsLogin()
    {
      CmdSocket.Send(new Request() { Func = Functions.FuncIsLogin }.ToByteArray());
      var response = GetResponse(CmdSocket);
      return response.Status == 1;
    }

    public async Task LoginWaitAsync()
    {
      while (!IsLogin())
      {
        await Task.Delay(1000);
      }
    }

    /// <summary>
    /// 开启消息接收服务
    /// param pyq bool 
    /// return int32 0 为成功，其他失败
    /// </summary>
    /// <param name="then">消息调用方法</param>
    /// <param name="func"></param>
    /// <param name="pyq">是否接收朋友圈消息</param>
    /// <returns></returns>
    public async Task<Response> EnableRecvTxt(Action<Response> then, Action<Exception> err = null, bool pyq = false)
    {
      CmdSocket.Send(new Request() { Func = Functions.FuncEnableRecvTxt, Flag = pyq }.ToByteArray());
      var response = GetResponse(CmdSocket);

      if (MsgSocket != null)
      {
        MsgSocket.Dispose();
        MsgSocket = null;
      }

      await Task.Run(() =>
       {
         try
         {
           MsgSocket?.Dispose();
           MsgSocket = Factory.PairOpen().ThenDial(msgUrl).Unwrap();
           while (MsgSocket != null)
           {
             var responseMsg = GetResponse(MsgSocket);
             then.Invoke(responseMsg);
           }
         }
         catch (Exception ex)
         {
           err?.Invoke(ex);
         }
       }).ConfigureAwait(false);

      return response;
    }

    // 停止消息接收服务
    // return int32 0 为成功，其他失败
    public Response DisableRecvTxt()
    {
      CmdSocket.Send(new Request() { Func = Functions.FuncDisableRecvTxt }.ToByteArray());
      var response = GetResponse(CmdSocket);
      if (response.Status == 0)
      {

      }
      return response;
    }

    private static Response GetResponse(IPairSocket socket)
    {
      var recvMsg = socket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      return Response.Parser.ParseFrom(recvData);
    }

    public int SendText(string msg, string receiver, string at = "")
    {
      var request1 = new Request() { Func = Functions.FuncSendTxt, Txt = new TextMsg() { Msg = msg, Receiver = receiver, Aters = at } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);
      return response.Status;
    }

    public int SendImg(string imgpath, string receiver)
    {
      var request1 = new Request() { Func = Functions.FuncSendImg, File = new PathMsg() { Path = imgpath, Receiver = receiver } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      return response.Status;
    }
    public int SendFile(string imgpath, string receiver)
    {
      var request1 = new Request() { Func = Functions.FuncSendFile, File = new PathMsg() { Path = imgpath, Receiver = receiver } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      return response.Status;
    }
    public int SendEmotion(string imgpath, string receiver)
    {
      var request1 = new Request() { Func = Functions.FuncSendEmotion, File = new PathMsg { Path = imgpath, Receiver = receiver } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      return response.Status;
    }

    public int ForwardMsg(ulong msgid, string receiver, int count = 3)
    {
      var request1 = new Request() { Func = Functions.FuncForwardMsg, Fm = new ForwardMsg { Id = msgid, Receiver = receiver } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      if (response.Status != 1 && count != 0)
      {
        return ForwardMsg(msgid, receiver, count - 1);
      }

      return response.Status;
    }

    /// <summary>
    /// 发送卡片消息
    /// </summary>
    /// <param name="name">显示名字</param>
    /// <param name="account">公众号 id</param>
    /// <param name="title">标题</param>
    /// <param name="digest">摘要</param>
    /// <param name="url">url</param>
    /// <param name="thumburl">略缩图</param>
    /// <param name="receiver">接收人</param>
    /// <returns></returns>
    public int SendRichText(string name, string account, string title, string digest, string url, string thumburl, string receiver)
    {
      var request1 = new Request() { Func = Functions.FuncSendRichTxt, Rt = new RichText() { Name = name, Account = account, Title = title, Digest = digest, Url = url, Thumburl = thumburl, Receiver = receiver } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      return response.Status;
    }

    // 获取完整通讯录
    // return []*RpcContact 完整通讯录
    public List<RpcContact> GetContacts()
    {
      Request request1 = new Request() { Func = Functions.FuncGetContacts };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      return response?.Contacts?.Contacts.ToList();
    }

    // 获取好友列表
    // return []*RpcContact 好友列表
    public List<RpcContact> GetFriends()
    {
      var result = new List<RpcContact>();
      var data = GetContacts();

      foreach (var item in data)
      {
        if (CmdHelper.ContactType(item.Wxid) == "好友")
        {
          result.Add(item);
        }
      }

      return result;
    }
    // 获取群聊列表
    public List<RpcContact> GetChatRooms()
    {
      var result = new List<RpcContact>();
      var data = GetContacts();
      if (data != null)
      {
        foreach (var item in data)
        {
          if (CmdHelper.ContactType(item.Wxid) == "群聊")
          {
            result.Add(item);
          }
        }

      }
      return result;
    }

    // 从数据库文件中获取群信息
    public List<IDNameModel> GetChatRoomsByDB()
    {
      var result = new List<IDNameModel>();
      var sessionlist = DbSqlQuery("MicroMsg.db", "SELECT strUsrName,strNickName FROM Session;");

      for (int i = 0; i < sessionlist.Rows.Count; i++)
      {
        var id = sessionlist.Rows[i]["strUsrName"].ToString();

        if (id.EndsWith("@chatroom") && !result.Any(p => p.Id == id))
        {
          result.Add(new IDNameModel() { Id = id, Name = sessionlist.Rows[i]["strNickName"].ToString() });
        }

      }
      return result;
    }

    public RpcContact GetInfoByWxid(string wxid)
    {
      Request request1 = new Request() { Func = Functions.FuncGetContactInfo, Str = wxid };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      if (response.Contacts?.Contacts?.Count > 0)
      {
        return response.Contacts.Contacts[0];
      }

      return null;
    }

    public DataTable DbSqlQuery(string db, string sql)
    {
      Request request1 = new Request() { Func = Functions.FuncExecDbQuery, Query = new DbQuery() { Db = db, Sql = sql } };
      CmdSocket.Send(request1.ToByteArray());

      var recvMsg = CmdSocket.RecvMsg().Unwrap();
      var recvData = recvMsg.AsSpan().ToArray();
      var response = Response.Parser.ParseFrom(recvData);

      DataTable dt = new DataTable();

      if (response.Rows != null)
      {
        foreach (DbRow row in response.Rows?.Rows)
        {
          var nrow = dt.NewRow();
          foreach (var item in row.Fields)
          {
            if (!dt.Columns.Contains(item.Column))
            {
              dt.Columns.Add(item.Column);
            }
            if (item.Type == 4)
            {
              nrow[item.Column] = item.Content.ToBase64();
            }
            else
            {
              nrow[item.Column] = item.Content.ToStringUtf8();
            }
          }
          dt.Rows.Add(nrow);
        }

      }

      return dt;
    }

    // 获取群成员昵称
    // param wxid string wxid
    // param roomid string 群的 id
    // return string 群成员昵称
    public string GetAliasInChatRoom(string wxid, string roomid)
    {
      var nickName = "";

      var userlist = DbSqlQuery("MicroMsg.db", "SELECT NickName FROM Contact WHERE UserName = '" + wxid + "';");
      if (userlist.Rows.Count > 0)
      {
        nickName = userlist.Rows[0]["NickName"].ToString();
      }

      var roomList = DbSqlQuery("MicroMsg.db", "SELECT RoomData FROM ChatRoom WHERE ChatRoomName = '" + roomid + "';");
      if (roomList.Rows.Count == 0 || userlist.Rows[0][0] == null)
      {
        return nickName;
      }
      //userlist.Rows[0][0]
      var str = roomList.Rows[0][0].ToString();
      var data = Convert.FromBase64String(roomList.Rows[0][0].ToString());
      var roomData = RoomData.Parser.ParseFrom(data);

      foreach (var item in roomData.Members)
      {
        if (item.Wxid == wxid)
        {
          if (!string.IsNullOrEmpty(item.Name))
          {
            nickName = item.Name;
          }
          break;
        }
      }

      return nickName;
    }

    // 获取群成员昵称
    // param wxid string wxid
    // param roomid string 群的 id
    // return string 群成员昵称
    public string GetAlias(string wxid)
    {
      var nickName = wxid;

      var userlist = DbSqlQuery("MicroMsg.db", "SELECT NickName FROM Contact WHERE UserName = '" + wxid + "';");
      if (userlist.Rows.Count > 0)
      {
        nickName = userlist.Rows[0]["NickName"].ToString();
      }
      return nickName;
    }

    public void Dispose()
    {
      CmdSocket?.Dispose();
      MsgSocket?.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}