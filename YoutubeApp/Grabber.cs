using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp;

public partial class Grabber : ObservableObject
{
    private readonly Youtube _youtube;
    private readonly IMessenger _messenger;
    private static int _nextJobId = 1;

    [ObservableProperty] private int _activeJobCount;

    public ObservableCollection<GrabberJob> GrabberJobs { get; } = new();

    [ObservableProperty] private bool _grabberHasError;

    public int? GrabberJobWindowId { get; set; }


    public Grabber(Youtube youtube, IMessenger messenger)
    {
        _youtube = youtube;
        _messenger = messenger;
        GrabberJob.Error += GrabberJob_Error;
        GrabberJob.Cancelled += GrabberJob_Cancelled;
        GrabberJob.Failed += GrabberJob_Failed;
        GrabberJob.Finished += GrabberJob_Finished;
        GrabberJob.Retry += GrabberJob_Retry;
    }

    public void AddJob(List<string> videos, List<string> playlists, string savePath, Channel? owner = null)
    {
        var id = _nextJobId++;
        var title = $"#{id}";
        if (owner is not null)
            title += $" | {owner.Title}";
        var job = new GrabberJob(id, videos, playlists, savePath, title, _youtube);
        GrabberJobs.Insert(0, job);
        ActiveJobCount++;
        _ = _youtube.GetVideosAsync(job);
    }

    // GrabberJob Events
    private void GrabberJob_Finished(object? sender, EventArgs e)
    {
        ActiveJobCount--;
    }

    private void GrabberJob_Failed(object? sender, EventArgs e)
    {
        ActiveJobCount--;
    }

    private void GrabberJob_Error(object? sender, EventArgs e)
    {
        var job = (GrabberJob)sender!;
        var grabberListIsOpen = _messenger.Send<GrabberListIsOpenMessage>().Response;
        if (grabberListIsOpen || GrabberJobWindowId == job.Id)
            return;
        GrabberHasError = true;
    }

    private void GrabberJob_Retry(object? sender, EventArgs e)
    {
        ActiveJobCount++;
    }

    private void GrabberJob_Cancelled(object? sender, CancelledEventArgs e)
    {
        var job = (GrabberJob)sender!;
        GrabberJobs.Remove(job);
        if (e.IsActive) ActiveJobCount--;
        if (GrabberJobs.Count == 0)
        {
            _messenger.Send(new GrabberListCloseMessage());
        }
    }
}