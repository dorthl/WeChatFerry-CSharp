
using WeChatFerry;

Console.WriteLine("正在载入 WeChatFerry ...");

var pid = 6666;
using var server = new WeChatFerryServer(pid);
Console.WriteLine("启动 WeChatFerry 成功!");

var client = new WeChatFerryClient(pid);
Console.WriteLine("连接 WeChatFerry 中...");

if (!client.IsValid())
{
  Console.WriteLine("连接 WeChatFerry 失败! 请手动关闭微信后重试");
  return;
}

Console.WriteLine("连接 WeChatFerry 成功! 请登录微信后继续...");

await client.LoginWaitAsync();

Console.WriteLine("检查登录成功 ... 监听消息");
await client.EnableRecvTxt((response) =>
{
  Console.WriteLine($"收到消息 Status:{response.Status},{response.Wxmsg}");
});

Console.WriteLine("启动成功等待接受消息...");
// 等待
Console.ReadLine();
