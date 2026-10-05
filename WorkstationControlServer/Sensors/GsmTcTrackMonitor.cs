#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WorkstationControlServer.Sensors
{
    public sealed record TrackMetadata(string Title, string Artist, byte[] AlbumArt);

    public sealed class GsmTcTrackMonitor : IDisposable
    {
        private readonly SynchronizationContext? _synchronizationContext =
            SynchronizationContext.Current;

        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _session;
        private int _requestId;
        private bool _disposed;

        public event Action<TrackMetadata>? MetadataChanged;
        public event Action<Exception>? ErrorOccurred;

        public async Task StartAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GsmTcTrackMonitor));
            }

            if (_manager != null)
            {
                return;
            }

            GlobalSystemMediaTransportControlsSessionManager manager =
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            if (_disposed)
            {
                return;
            }

            _manager = manager;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionsChanged;

            AttachCurrentSession();
        }

        private void OnCurrentSessionChanged(
            GlobalSystemMediaTransportControlsSessionManager sender,
            object args)
        {
            Dispatch(AttachCurrentSession);
        }

        private void OnSessionsChanged(
            GlobalSystemMediaTransportControlsSessionManager sender,
            SessionsChangedEventArgs args)
        {
            Dispatch(AttachCurrentSession);
        }

        private void AttachCurrentSession()
        {
            if (_disposed || _manager == null)
            {
                return;
            }

            int requestId = Interlocked.Increment(ref _requestId);

            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            }

            _session = _manager.GetCurrentSession();

            if (_session == null)
            {
                Publish(
                    new TrackMetadata(string.Empty, string.Empty, null),
                    requestId);
                return;
            }

            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _ = PublishMetadataAsync(_session);
        }

        private void OnMediaPropertiesChanged(
            GlobalSystemMediaTransportControlsSession sender,
            MediaPropertiesChangedEventArgs args)
        {
            Dispatch(OnMediaPropertiesChangedCallback);
        }

        private void OnMediaPropertiesChangedCallback()
        {
            if (_session != null)
            {
                _ = PublishMetadataAsync(_session);
            }
        }

        private async Task PublishMetadataAsync(
            GlobalSystemMediaTransportControlsSession session)
        {
            int requestId = Interlocked.Increment(ref _requestId);

            try
            {
                var properties = await session.TryGetMediaPropertiesAsync();

                if (!IsCurrentRequest(session, requestId))
                {
                    return;
                }

                byte[]? albumArt = null;

                try
                {
                    albumArt = await ReadThumbnailAsync(properties.Thumbnail);
                }
                catch (Exception exception)
                {
                    PublishError(exception);
                }

                if (IsCurrentRequest(session, requestId))
                {
                    Publish(
                        new TrackMetadata(
                            properties.Title ?? string.Empty,
                            properties.Artist ?? string.Empty,
                            albumArt),
                        requestId);
                }
            }
            catch (Exception exception)
            {
                if (IsCurrentRequest(session, requestId))
                {
                    PublishError(exception);
                }
            }
        }

        private bool IsCurrentRequest(
            GlobalSystemMediaTransportControlsSession session,
            int requestId)
        {
            return !_disposed
                && ReferenceEquals(session, _session)
                && requestId == Volatile.Read(ref _requestId);
        }

        private static async Task<byte[]?> ReadThumbnailAsync(
            IRandomAccessStreamReference thumbnail)
        {
            if (thumbnail == null)
            {
                return null;
            }

            try
            {
                using var stream = await thumbnail.OpenReadAsync();

                if (stream.Size == 0 || stream.Size > int.MaxValue)
                {
                    return null;
                }

                using var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);

                var bytes = new byte[(int)stream.Size];
                reader.ReadBytes(bytes);

                return bytes;
            }
            catch
            {
                return null;
            }
        }

        private void Publish(TrackMetadata metadata, int requestId)
        {
            PublishState state = new PublishState(this, metadata, requestId);
            Dispatch(state.Execute);
        }

        private class PublishState
        {
            private readonly GsmTcTrackMonitor _owner;
            private readonly TrackMetadata _metadata;
            private readonly int _requestId;

            public PublishState(GsmTcTrackMonitor owner, TrackMetadata metadata, int requestId)
            {
                _owner = owner;
                _metadata = metadata;
                _requestId = requestId;
            }

            public void Execute()
            {
                if (!_owner._disposed && _requestId == Volatile.Read(ref _owner._requestId))
                {
                    _owner.MetadataChanged?.Invoke(_metadata);
                }
            }
        }

        private void PublishError(Exception exception)
        {
            PublishErrorState state = new PublishErrorState(this, exception);
            Dispatch(state.Execute);
        }

        private class PublishErrorState
        {
            private readonly GsmTcTrackMonitor _owner;
            private readonly Exception _exception;

            public PublishErrorState(GsmTcTrackMonitor owner, Exception exception)
            {
                _owner = owner;
                _exception = exception;
            }

            public void Execute()
            {
                if (!_owner._disposed)
                {
                    _owner.ErrorOccurred?.Invoke(_exception);
                }
            }
        }

        private void Dispatch(Action action)
        {
            if (_synchronizationContext == null)
            {
                action();
                return;
            }

            _synchronizationContext.Post(PostCallback, action);
        }

        private static void PostCallback(object? state)
        {
            if (state is Action action)
            {
                action();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Increment(ref _requestId);

            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager.SessionsChanged -= OnSessionsChanged;
            }

            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            }
        }
    }
}