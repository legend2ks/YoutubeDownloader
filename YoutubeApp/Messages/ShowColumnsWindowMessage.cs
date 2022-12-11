using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace YoutubeApp.Messages;

public class ShowColumnsWindowMessage : AsyncRequestMessage<IEnumerable<ColumnConfig>?>
{
}