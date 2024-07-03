﻿using Serilog;
using WebSocketSharp.Server;
using WhackerLinkCommonLib.Interfaces;
using WhackerLinkServer.Models;
using WhackerLinkServer.Managers;
using WhackerLinkCommonLib.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using WhackerLinkServer;

#nullable disable

public class Master : IMasterService
{
    private Config.MasterConfig config;
    private WebSocketServer server;
    private RidAclManager aclManager;
    private RestApiServer restServer;
    private Timer aclReloadTimer;
    private AffiliationsManager affiliationsManager;
    private VoiceChannelManager voiceChannelManager;

    public Master(Config.MasterConfig config)
    {
        this.config = config;
        this.aclManager = new RidAclManager(config.RidAcl.Enabled);
        this.affiliationsManager = new AffiliationsManager();
        this.voiceChannelManager = new VoiceChannelManager();
    }

    public List<Affiliation> GetAffiliations()
    {
        return affiliationsManager.GetAffiliations();
    }

    public List<VoiceChannel> GetVoiceChannels()
    {
        return voiceChannelManager.GetVoiceChannels();
    }

    public List<string> GetAvailableVoiceChannels()
    {
        return config.VoiceChannels;
    }

    public List<RidAclEntry> GetRidAcl()
    {
        return aclManager.ridAclEntries;
    }

    public bool GetRidAclEnabled()
    {
        return aclManager.GetAclEnabled();
    }

    public void Start()
    {
        try
        {
            Log.Information("Starting Master {Name}", config.Name);

            aclManager.Load(config.RidAcl.Path);

            if (config.RidAcl.ReloadInterval > 0)
            {
                aclReloadTimer = new Timer(ReloadAclFile, null, 0, config.RidAcl.ReloadInterval * 1000);
            }
            else
            {
                Log.Information("ACL Auto reload disabled");
            }

            if (config.Rest.Enabled)
            {
                restServer = new RestApiServer(this, config.Rest.Address, config.Rest.Port);
                restServer.Start();
            }

            server = new WebSocketServer($"ws://{config.Address}:{config.Port}");
            server.AddWebSocketService<ClientHandler>("/client", () => new ClientHandler(config, aclManager, affiliationsManager, voiceChannelManager));
            server.Start();

            Log.Information("Master {Name} Listening on port {Port}", config.Name, config.Port);
        }
        catch (IOException ex)
        {
            Log.Error(ex, "IO Error");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private void ReloadAclFile(object state)
    {
        try
        {
            aclManager.Load(config.RidAcl.Path);
            Log.Information("Reloaded RID ACL entries from {Path}", config.RidAcl.Path);
        }
        catch (Exception ex)
        {
            Log.Error("Error reloading RID ACL: {Message}", ex.Message);
        }
    }
}