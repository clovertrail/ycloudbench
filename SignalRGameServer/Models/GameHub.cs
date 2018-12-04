using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Orleans;
using SignalRGameServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRGameServer.Models
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GameHub : Hub
    {
        private readonly TelemetryClient telemetryClient = new TelemetryClient();

        //private readonly OrleansService orleansService;

        //public GameHub(OrleansService orleansService) : base()
        //{
        //    this.orleansService = orleansService;
        //}

        //public void BroadcastMessage(string name, string message)
        //{
        //    Clients.All.SendAsync("broadcastMessage", name, message);
        //}

        //public void Echo(string name, string message)
        //{
        //    Clients.Client(Context.ConnectionId).SendAsync("echo", name, message + " (echo from server)");
        //}

        public void SendMessage(string message)
        {
            Clients.Client(Context.ConnectionId).SendAsync("ReceiveMessage", Context.UserIdentifier, message);
        }

        public void PerformanceTest(object message)
        {
            telemetryClient.TrackTrace("PerformanceTest");
            Clients.Client(Context.ConnectionId).SendAsync("PerformanceTest", message);
        }

        public void Call(string method, IReadOnlyList<object> parameters)
        {
            //orleansService.CallRPCGrain("IPlayerGrain", Context.UserIdentifier, method, parameters.ToArray());
        }

        //public async void JoinGroup(string name, string groupName)
        //{
        //    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        //    await Clients.Group(groupName).SendAsync("echo", "_SYSTEM_", $"{name} joined {groupName} with connectionId {Context.ConnectionId}");
        //}

        //public async void LeaveGroup(string name, string groupName)
        //{
        //    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        //    await Clients.Client(Context.ConnectionId).SendAsync("echo", "_SYSTEM_", $"{name} leaved {groupName}");
        //    await Clients.Group(groupName).SendAsync("echo", "_SYSTEM_", $"{name} leaved {groupName}");
        //}

        //public void SendGroup(string name, string groupName, string message)
        //{
        //    Clients.Group(groupName).SendAsync("echo", name, message);
        //}

        //public void SendGroups(string name, IReadOnlyList<string> groups, string message)
        //{
        //    Clients.Groups(groups).SendAsync("echo", name, message);
        //}

        //public void SendGroupExcept(string name, string groupName, IReadOnlyList<string> connectionIdExcept, string message)
        //{
        //    Clients.GroupExcept(groupName, connectionIdExcept).SendAsync("echo", name, message);
        //}

        //public void SendUser(string name, string userId, string message)
        //{
        //    Clients.User(userId).SendAsync("echo", name, message);
        //}

        //public void SendUsers(string name, IReadOnlyList<string> userIds, string message)
        //{
        //    Clients.Users(userIds).SendAsync("echo", name, message);
        //}

        public override Task OnConnectedAsync()
        {
            telemetryClient.TrackTrace("Connected");
            Clients.Client(Context.ConnectionId).SendAsync("Connected");
            //return orleansService.CallRPCGrain("IPlayerGrain", Context.UserIdentifier, "Connected", null);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            telemetryClient.TrackTrace("DisConnected");
            //return orleansService.CallRPCGrain("IPlayerGrain", Context.UserIdentifier, "DisConnected", null);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
