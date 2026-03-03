// =============================================================================
// StingBIM.AI.Collaboration - Web Viewer Service
// Lightweight web-based BIM viewer for clients, reviewers, and site teams
// No Revit license required - supports IFC, glTF, and StingBIM formats
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Collaboration.WebViewer
{
    /// <summary>
    /// Web-based BIM viewer service that allows clients and reviewers
    /// to view and comment on models without Revit installation.
    /// </summary>
    public class WebViewerService : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Server
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // State
        private readonly ConcurrentDictionary<string, PublishedModel> _publishedModels = new();
        private readonly ConcurrentDictionary<string, ViewerSession> _sessions = new();
        private readonly ConcurrentDictionary<string, List<ModelComment>> _comments = new();
        private readonly ConcurrentDictionary<string, List<ModelMarkup>> _markups = new();

        // Configuration
        private readonly WebViewerConfig _config;

        // Events
        public event EventHandler<ModelPublishedEventArgs>? ModelPublished;
        public event EventHandler<CommentAddedEventArgs>? CommentAdded;
        public event EventHandler<MarkupAddedEventArgs>? MarkupAdded;
        public event EventHandler<ViewerConnectedEventArgs>? ViewerConnected;

        public bool IsRunning => _isRunning;
        public int PublishedModelCount => _publishedModels.Count;
        public int ActiveSessionCount => _sessions.Count;

        public WebViewerService(WebViewerConfig? config = null)
        {
            _config = config ?? new WebViewerConfig();
        }

        #region Server Lifecycle

        /// <summary>
        /// Start the web viewer service
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://{_config.Host}:{_config.Port}/");
                _httpListener.Start();

                _isRunning = true;
                Logger.Info($"Web Viewer Service started on http://{_config.Host}:{_config.Port}");

                // Start accepting requests
                await AcceptRequestsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start Web Viewer Service");
                throw;
            }
        }

        /// <summary>
        /// Stop the web viewer service
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _httpListener?.Stop();
            _isRunning = false;

            Logger.Info("Web Viewer Service stopped");
        }

        private async Task AcceptRequestsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _httpListener!.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error accepting request");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var method = context.Request.HttpMethod;

            try
            {
                // Route requests
                if (path == "/" || path == "/index.html")
                {
                    await ServeViewerAppAsync(context);
                }
                else if (path.StartsWith("/api/models"))
                {
                    await HandleModelApiAsync(context, method, path);
                }
                else if (path.StartsWith("/api/comments"))
                {
                    await HandleCommentApiAsync(context, method, path);
                }
                else if (path.StartsWith("/api/markups"))
                {
                    await HandleMarkupApiAsync(context, method, path);
                }
                else if (path.StartsWith("/viewer/"))
                {
                    await HandleViewerRequestAsync(context, path);
                }
                else if (path.StartsWith("/assets/"))
                {
                    await ServeStaticAssetAsync(context, path);
                }
                else if (path.StartsWith("/ws"))
                {
                    if (context.Request.IsWebSocketRequest)
                    {
                        await HandleWebSocketAsync(context, ct);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error handling request: {path}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        #endregion

        #region Model Publishing

        /// <summary>
        /// Publish a model for web viewing
        /// </summary>
        public async Task<PublishedModel> PublishModelAsync(
            string projectId,
            string modelName,
            Stream modelData,
            ModelFormat format,
            PublishOptions? options = null)
        {
            options ??= new PublishOptions();

            var modelId = GenerateModelId();
            var accessCode = GenerateAccessCode();

            // Create storage directory
            var modelDir = Path.Combine(_config.StoragePath, modelId);
            Directory.CreateDirectory(modelDir);

            // Save model file
            var fileName = $"model.{format.ToString().ToLower()}";
            var filePath = Path.Combine(modelDir, fileName);
            using (var fileStream = File.Create(filePath))
            {
                await modelData.CopyToAsync(fileStream);
            }

            // Generate thumbnail if possible
            string? thumbnailPath = null;
            if (options.GenerateThumbnail)
            {
                thumbnailPath = await GenerateThumbnailAsync(filePath, modelDir);
            }

            // Generate lightweight version for web
            var webModelPath = await ConvertForWebAsync(filePath, modelDir, format);

            var published = new PublishedModel
            {
                ModelId = modelId,
                ProjectId = projectId,
                ModelName = modelName,
                Format = format,
                AccessCode = accessCode,
                FilePath = filePath,
                WebModelPath = webModelPath,
                ThumbnailPath = thumbnailPath,
                PublishedAt = DateTime.UtcNow,
                ExpiresAt = options.ExpirationDate,
                IsPublic = options.IsPublic,
                AllowComments = options.AllowComments,
                AllowMarkups = options.AllowMarkups,
                RequireAuthentication = options.RequireAuthentication,
                PublishedBy = options.PublishedBy
            };

            _publishedModels[modelId] = published;

            // Generate share URL
            published.ShareUrl = $"http://{_config.PublicHost}:{_config.Port}/viewer/{modelId}";
            if (!published.IsPublic)
            {
                published.ShareUrl += $"?code={accessCode}";
            }

            ModelPublished?.Invoke(this, new ModelPublishedEventArgs(published));
            Logger.Info($"Published model: {modelName} (ID: {modelId})");

            return published;
        }

        /// <summary>
        /// Update a published model with new geometry
        /// </summary>
        public async Task UpdateModelAsync(string modelId, Stream newData)
        {
            if (!_publishedModels.TryGetValue(modelId, out var model))
            {
                throw new ArgumentException($"Model not found: {modelId}");
            }

            // Save new version
            using (var fileStream = File.Create(model.FilePath))
            {
                await newData.CopyToAsync(fileStream);
            }

            // Regenerate web version
            var modelDir = Path.GetDirectoryName(model.FilePath)!;
            model.WebModelPath = await ConvertForWebAsync(model.FilePath, modelDir, model.Format);
            model.LastUpdated = DateTime.UtcNow;
            model.Version++;

            // Notify connected viewers
            await NotifyModelUpdatedAsync(modelId);

            Logger.Info($"Updated model: {model.ModelName} (v{model.Version})");
        }

        /// <summary>
        /// Unpublish a model
        /// </summary>
        public void UnpublishModel(string modelId)
        {
            if (_publishedModels.TryRemove(modelId, out var model))
            {
                // Clean up files
                var modelDir = Path.GetDirectoryName(model.FilePath);
                if (modelDir != null && Directory.Exists(modelDir))
                {
                    Directory.Delete(modelDir, true);
                }

                Logger.Info($"Unpublished model: {model.ModelName}");
            }
        }

        /// <summary>
        /// Get published model by ID
        /// </summary>
        public PublishedModel? GetModel(string modelId)
        {
            return _publishedModels.GetValueOrDefault(modelId);
        }

        /// <summary>
        /// List all published models for a project
        /// </summary>
        public IEnumerable<PublishedModel> GetProjectModels(string projectId)
        {
            return _publishedModels.Values.Where(m => m.ProjectId == projectId);
        }

        private async Task<string> ConvertForWebAsync(string sourcePath, string outputDir, ModelFormat format)
        {
            // Convert to glTF/glb for web viewing
            var outputPath = Path.Combine(outputDir, "model.glb");

            // In production, would use tools like:
            // - IfcConvert for IFC
            // - Assimp for various formats
            // - Custom Revit export for native files

            // For now, if it's already glTF, just copy
            if (format == ModelFormat.GLTF || format == ModelFormat.GLB)
            {
                File.Copy(sourcePath, outputPath, true);
            }
            else
            {
                // Placeholder for conversion logic
                // Would call appropriate converter based on format
                File.Copy(sourcePath, outputPath, true);
            }

            return outputPath;
        }

        private async Task<string?> GenerateThumbnailAsync(string modelPath, string outputDir)
        {
            // Would generate 3D thumbnail using headless renderer
            // For now, return null
            return null;
        }

        private string GenerateModelId()
        {
            return $"mdl_{Guid.NewGuid():N}"[..16];
        }

        private string GenerateAccessCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(12);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_")[..16];
        }

        #endregion

        #region Comments & Markups

        /// <summary>
        /// Add a comment to a model
        /// </summary>
        public ModelComment AddComment(string modelId, string author, string content,
            ViewPosition? position = null, string? elementId = null)
        {
            if (!_publishedModels.TryGetValue(modelId, out var model) || !model.AllowComments)
            {
                throw new InvalidOperationException("Cannot add comment to this model");
            }

            var comment = new ModelComment
            {
                CommentId = Guid.NewGuid().ToString(),
                ModelId = modelId,
                Author = author,
                Content = content,
                Position = position,
                ElementId = elementId,
                CreatedAt = DateTime.UtcNow
            };

            if (!_comments.ContainsKey(modelId))
                _comments[modelId] = new List<ModelComment>();

            _comments[modelId].Add(comment);

            // Notify connected viewers
            _ = NotifyCommentAddedAsync(modelId, comment);

            CommentAdded?.Invoke(this, new CommentAddedEventArgs(comment));
            return comment;
        }

        /// <summary>
        /// Get comments for a model
        /// </summary>
        public IEnumerable<ModelComment> GetComments(string modelId)
        {
            return _comments.GetValueOrDefault(modelId, new List<ModelComment>());
        }

        /// <summary>
        /// Add a markup to a model
        /// </summary>
        public ModelMarkup AddMarkup(string modelId, string author, MarkupData data)
        {
            if (!_publishedModels.TryGetValue(modelId, out var model) || !model.AllowMarkups)
            {
                throw new InvalidOperationException("Cannot add markup to this model");
            }

            var markup = new ModelMarkup
            {
                MarkupId = Guid.NewGuid().ToString(),
                ModelId = modelId,
                Author = author,
                Data = data,
                CreatedAt = DateTime.UtcNow
            };

            if (!_markups.ContainsKey(modelId))
                _markups[modelId] = new List<ModelMarkup>();

            _markups[modelId].Add(markup);

            // Notify connected viewers
            _ = NotifyMarkupAddedAsync(modelId, markup);

            MarkupAdded?.Invoke(this, new MarkupAddedEventArgs(markup));
            return markup;
        }

        /// <summary>
        /// Get markups for a model
        /// </summary>
        public IEnumerable<ModelMarkup> GetMarkups(string modelId)
        {
            return _markups.GetValueOrDefault(modelId, new List<ModelMarkup>());
        }

        #endregion

        #region HTTP Handlers

        private async Task ServeViewerAppAsync(HttpListenerContext context)
        {
            var html = GenerateViewerHtml();
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
        }

        private async Task HandleViewerRequestAsync(HttpListenerContext context, string path)
        {
            // Extract model ID from path: /viewer/{modelId}
            var parts = path.Split('/');
            if (parts.Length < 3)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var modelId = parts[2];
            var accessCode = context.Request.QueryString["code"];

            if (!_publishedModels.TryGetValue(modelId, out var model))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // Validate access
            if (!model.IsPublic && model.AccessCode != accessCode)
            {
                context.Response.StatusCode = 403;
                return;
            }

            // Check expiration
            if (model.ExpiresAt.HasValue && DateTime.UtcNow > model.ExpiresAt.Value)
            {
                context.Response.StatusCode = 410; // Gone
                return;
            }

            // Serve viewer with model data
            var html = GenerateModelViewerHtml(model);
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
        }

        private async Task HandleModelApiAsync(HttpListenerContext context, string method, string path)
        {
            // /api/models - list models
            // /api/models/{id} - get model
            // /api/models/{id}/geometry - get geometry data

            var parts = path.Split('/');
            if (parts.Length == 3 && method == "GET")
            {
                // List models
                var models = _publishedModels.Values.Where(m => m.IsPublic).Select(m => new
                {
                    m.ModelId,
                    m.ModelName,
                    m.Format,
                    m.PublishedAt,
                    m.ThumbnailPath
                });
                await WriteJsonAsync(context.Response, models);
            }
            else if (parts.Length >= 4)
            {
                var modelId = parts[3];

                if (!_publishedModels.TryGetValue(modelId, out var model))
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                if (parts.Length == 4 && method == "GET")
                {
                    // Get model info
                    await WriteJsonAsync(context.Response, new
                    {
                        model.ModelId,
                        model.ModelName,
                        model.Format,
                        model.PublishedAt,
                        model.Version,
                        model.AllowComments,
                        model.AllowMarkups
                    });
                }
                else if (parts.Length == 5 && parts[4] == "geometry" && method == "GET")
                {
                    // Serve geometry file
                    if (File.Exists(model.WebModelPath))
                    {
                        context.Response.ContentType = "model/gltf-binary";
                        using var fs = File.OpenRead(model.WebModelPath);
                        await fs.CopyToAsync(context.Response.OutputStream);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                    }
                }
            }
        }

        private async Task HandleCommentApiAsync(HttpListenerContext context, string method, string path)
        {
            var parts = path.Split('/');
            if (parts.Length < 4)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var modelId = parts[3];

            if (method == "GET")
            {
                var comments = GetComments(modelId);
                await WriteJsonAsync(context.Response, comments);
            }
            else if (method == "POST")
            {
                var body = await ReadBodyAsync(context.Request);
                var request = JsonConvert.DeserializeObject<AddCommentRequest>(body);

                if (request == null)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                var comment = AddComment(modelId, request.Author, request.Content,
                    request.Position, request.ElementId);
                await WriteJsonAsync(context.Response, comment);
            }
        }

        private async Task HandleMarkupApiAsync(HttpListenerContext context, string method, string path)
        {
            var parts = path.Split('/');
            if (parts.Length < 4)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var modelId = parts[3];

            if (method == "GET")
            {
                var markups = GetMarkups(modelId);
                await WriteJsonAsync(context.Response, markups);
            }
            else if (method == "POST")
            {
                var body = await ReadBodyAsync(context.Request);
                var request = JsonConvert.DeserializeObject<AddMarkupRequest>(body);

                if (request == null)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                var markup = AddMarkup(modelId, request.Author, request.Data);
                await WriteJsonAsync(context.Response, markup);
            }
        }

        private async Task ServeStaticAssetAsync(HttpListenerContext context, string path)
        {
            // Serve JS, CSS, and other assets
            var assetPath = Path.Combine(_config.AssetsPath, path.TrimStart('/'));

            if (File.Exists(assetPath))
            {
                var contentType = GetContentType(assetPath);
                context.Response.ContentType = contentType;

                using var fs = File.OpenRead(assetPath);
                await fs.CopyToAsync(context.Response.OutputStream);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }

        #endregion

        #region WebSocket Handlers

        private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
        {
            var modelId = context.Request.QueryString["model"];
            if (string.IsNullOrEmpty(modelId) || !_publishedModels.ContainsKey(modelId))
            {
                context.Response.StatusCode = 400;
                return;
            }

            var wsContext = await context.AcceptWebSocketAsync(null);
            var ws = wsContext.WebSocket;

            var session = new ViewerSession
            {
                SessionId = Guid.NewGuid().ToString(),
                ModelId = modelId,
                WebSocket = ws,
                ConnectedAt = DateTime.UtcNow,
                RemoteEndpoint = context.Request.RemoteEndPoint?.ToString() ?? ""
            };

            _sessions[session.SessionId] = session;
            ViewerConnected?.Invoke(this, new ViewerConnectedEventArgs(session));

            try
            {
                // Send current state
                await SendViewerStateAsync(session);

                // Process messages
                var buffer = new byte[4096];
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleViewerMessageAsync(session, json);
                    }
                }
            }
            finally
            {
                _sessions.TryRemove(session.SessionId, out _);
            }
        }

        private async Task HandleViewerMessageAsync(ViewerSession session, string json)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<ViewerMessage>(json);
                if (message == null) return;

                switch (message.Type)
                {
                    case "camera_update":
                        // Broadcast camera position to other viewers (for sync viewing)
                        await BroadcastToModelViewersAsync(session.ModelId, json, session.SessionId);
                        break;

                    case "add_comment":
                        var commentData = JsonConvert.DeserializeObject<AddCommentRequest>(
                            message.Payload?.ToString() ?? "");
                        if (commentData != null)
                        {
                            AddComment(session.ModelId, commentData.Author, commentData.Content,
                                commentData.Position, commentData.ElementId);
                        }
                        break;

                    case "add_markup":
                        var markupData = JsonConvert.DeserializeObject<AddMarkupRequest>(
                            message.Payload?.ToString() ?? "");
                        if (markupData != null)
                        {
                            AddMarkup(session.ModelId, markupData.Author, markupData.Data);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling viewer message");
            }
        }

        private async Task SendViewerStateAsync(ViewerSession session)
        {
            // Send current comments and markups
            var state = new
            {
                comments = GetComments(session.ModelId),
                markups = GetMarkups(session.ModelId),
                viewers = _sessions.Values
                    .Where(s => s.ModelId == session.ModelId)
                    .Select(s => new { s.SessionId, s.UserName })
            };

            await SendToSessionAsync(session, new ViewerMessage
            {
                Type = "state",
                Payload = state
            });
        }

        private async Task NotifyModelUpdatedAsync(string modelId)
        {
            await BroadcastToModelViewersAsync(modelId, JsonConvert.SerializeObject(new ViewerMessage
            {
                Type = "model_updated",
                Payload = new { modelId }
            }), null);
        }

        private async Task NotifyCommentAddedAsync(string modelId, ModelComment comment)
        {
            await BroadcastToModelViewersAsync(modelId, JsonConvert.SerializeObject(new ViewerMessage
            {
                Type = "comment_added",
                Payload = comment
            }), null);
        }

        private async Task NotifyMarkupAddedAsync(string modelId, ModelMarkup markup)
        {
            await BroadcastToModelViewersAsync(modelId, JsonConvert.SerializeObject(new ViewerMessage
            {
                Type = "markup_added",
                Payload = markup
            }), null);
        }

        private async Task BroadcastToModelViewersAsync(string modelId, string message, string? excludeSession)
        {
            var sessions = _sessions.Values
                .Where(s => s.ModelId == modelId && s.SessionId != excludeSession);

            foreach (var session in sessions)
            {
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await session.WebSocket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                catch { }
            }
        }

        private async Task SendToSessionAsync(ViewerSession session, ViewerMessage message)
        {
            var json = JsonConvert.SerializeObject(message);
            var buffer = Encoding.UTF8.GetBytes(json);
            await session.WebSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        #endregion

        #region HTML Generation

        private string GenerateViewerHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>StingBIM Web Viewer</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #1a1a2e; color: #fff; }
        .header { background: #16213e; padding: 1rem 2rem; display: flex; justify-content: space-between; align-items: center; }
        .header h1 { font-size: 1.5rem; color: #4ecca3; }
        .models-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 2rem; padding: 2rem; }
        .model-card { background: #16213e; border-radius: 12px; overflow: hidden; transition: transform 0.2s; }
        .model-card:hover { transform: translateY(-4px); }
        .model-thumbnail { height: 180px; background: #0f3460; display: flex; align-items: center; justify-content: center; }
        .model-thumbnail svg { width: 64px; height: 64px; opacity: 0.5; }
        .model-info { padding: 1.5rem; }
        .model-name { font-size: 1.25rem; margin-bottom: 0.5rem; }
        .model-meta { color: #a0a0a0; font-size: 0.875rem; }
        .btn { background: #4ecca3; color: #1a1a2e; border: none; padding: 0.75rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; }
        .btn:hover { background: #3dbb91; }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🏗️ StingBIM Web Viewer</h1>
    </div>
    <div class=""models-grid"" id=""models"">
        <p style=""color: #a0a0a0; text-align: center; grid-column: 1/-1; padding: 4rem;"">
            Loading published models...
        </p>
    </div>
    <script>
        fetch('/api/models')
            .then(r => r.json())
            .then(models => {
                const container = document.getElementById('models');
                if (models.length === 0) {
                    container.innerHTML = '<p style=""color: #a0a0a0; text-align: center; grid-column: 1/-1; padding: 4rem;"">No models published yet.</p>';
                    return;
                }
                container.innerHTML = models.map(m => `
                    <div class=""model-card"">
                        <div class=""model-thumbnail"">
                            <svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 3L2 12h3v9h14v-9h3L12 3z""/></svg>
                        </div>
                        <div class=""model-info"">
                            <div class=""model-name"">${m.modelName}</div>
                            <div class=""model-meta"">Published ${new Date(m.publishedAt).toLocaleDateString()}</div>
                            <button class=""btn"" style=""margin-top: 1rem;"" onclick=""location.href='/viewer/${m.modelId}'"">Open Viewer</button>
                        </div>
                    </div>
                `).join('');
            });
    </script>
</body>
</html>";
        }

        private string GenerateModelViewerHtml(PublishedModel model)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{model.ModelName} - StingBIM Viewer</title>
    <script src=""https://cdn.babylonjs.com/babylon.js""></script>
    <script src=""https://cdn.babylonjs.com/loaders/babylonjs.loaders.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        #canvas {{ width: 100vw; height: 100vh; display: block; }}
        .toolbar {{ position: fixed; top: 1rem; left: 1rem; background: rgba(22, 33, 62, 0.95); padding: 1rem; border-radius: 12px; z-index: 100; }}
        .toolbar h2 {{ color: #4ecca3; font-size: 1rem; margin-bottom: 0.5rem; }}
        .toolbar-btn {{ background: #4ecca3; color: #1a1a2e; border: none; padding: 0.5rem 1rem; border-radius: 6px; margin: 0.25rem; cursor: pointer; font-size: 0.875rem; }}
        .toolbar-btn:hover {{ background: #3dbb91; }}
        .comments-panel {{ position: fixed; top: 1rem; right: 1rem; width: 320px; max-height: 60vh; background: rgba(22, 33, 62, 0.95); border-radius: 12px; z-index: 100; overflow: hidden; }}
        .comments-header {{ padding: 1rem; border-bottom: 1px solid #0f3460; display: flex; justify-content: space-between; align-items: center; }}
        .comments-header h3 {{ color: #4ecca3; }}
        .comments-list {{ max-height: 400px; overflow-y: auto; padding: 1rem; }}
        .comment {{ background: #0f3460; padding: 0.75rem; border-radius: 8px; margin-bottom: 0.5rem; }}
        .comment-author {{ color: #4ecca3; font-weight: 600; font-size: 0.875rem; }}
        .comment-text {{ color: #fff; margin-top: 0.25rem; }}
        .comment-time {{ color: #666; font-size: 0.75rem; margin-top: 0.25rem; }}
        .add-comment {{ padding: 1rem; border-top: 1px solid #0f3460; }}
        .add-comment textarea {{ width: 100%; background: #0f3460; border: none; color: #fff; padding: 0.5rem; border-radius: 6px; resize: none; }}
        .loading {{ position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); color: #4ecca3; font-size: 1.5rem; }}
    </style>
</head>
<body>
    <canvas id=""canvas""></canvas>
    <div class=""toolbar"">
        <h2>🏗️ {model.ModelName}</h2>
        <button class=""toolbar-btn"" onclick=""resetCamera()"">Reset View</button>
        <button class=""toolbar-btn"" onclick=""toggleWireframe()"">Wireframe</button>
        <button class=""toolbar-btn"" onclick=""fitToView()"">Fit All</button>
    </div>
    {(model.AllowComments ? @"
    <div class=""comments-panel"">
        <div class=""comments-header"">
            <h3>💬 Comments</h3>
            <span id=""comment-count"">0</span>
        </div>
        <div class=""comments-list"" id=""comments""></div>
        <div class=""add-comment"">
            <textarea id=""new-comment"" placeholder=""Add a comment..."" rows=""2""></textarea>
            <button class=""toolbar-btn"" style=""width: 100%; margin-top: 0.5rem;"" onclick=""addComment()"">Post Comment</button>
        </div>
    </div>" : "")}
    <div class=""loading"" id=""loading"">Loading model...</div>
    <script>
        const canvas = document.getElementById('canvas');
        const engine = new BABYLON.Engine(canvas, true);
        let scene, camera;
        let wireframe = false;

        const createScene = async () => {{
            scene = new BABYLON.Scene(engine);
            scene.clearColor = new BABYLON.Color4(0.1, 0.1, 0.18, 1);

            camera = new BABYLON.ArcRotateCamera('camera', Math.PI / 2, Math.PI / 3, 10, BABYLON.Vector3.Zero(), scene);
            camera.attachControl(canvas, true);
            camera.wheelPrecision = 50;
            camera.panningSensibility = 100;

            const light1 = new BABYLON.HemisphericLight('light1', new BABYLON.Vector3(0, 1, 0), scene);
            light1.intensity = 0.7;
            const light2 = new BABYLON.DirectionalLight('light2', new BABYLON.Vector3(-1, -2, -1), scene);
            light2.intensity = 0.5;

            try {{
                const result = await BABYLON.SceneLoader.ImportMeshAsync('', '/api/models/{model.ModelId}/', 'geometry', scene);
                document.getElementById('loading').style.display = 'none';
                fitToView();
            }} catch (e) {{
                document.getElementById('loading').textContent = 'Error loading model';
                console.error(e);
            }}

            return scene;
        }};

        const resetCamera = () => {{
            camera.alpha = Math.PI / 2;
            camera.beta = Math.PI / 3;
            camera.radius = 10;
            camera.target = BABYLON.Vector3.Zero();
        }};

        const toggleWireframe = () => {{
            wireframe = !wireframe;
            scene.meshes.forEach(m => m.material && (m.material.wireframe = wireframe));
        }};

        const fitToView = () => {{
            const bounds = scene.meshes.reduce((acc, m) => {{
                if (m.getBoundingInfo) {{
                    const b = m.getBoundingInfo().boundingBox;
                    acc.min = BABYLON.Vector3.Minimize(acc.min, b.minimumWorld);
                    acc.max = BABYLON.Vector3.Maximize(acc.max, b.maximumWorld);
                }}
                return acc;
            }}, {{ min: new BABYLON.Vector3(Infinity, Infinity, Infinity), max: new BABYLON.Vector3(-Infinity, -Infinity, -Infinity) }});

            const center = bounds.min.add(bounds.max).scale(0.5);
            const size = bounds.max.subtract(bounds.min).length();
            camera.target = center;
            camera.radius = size * 1.5;
        }};

        createScene().then(() => {{
            engine.runRenderLoop(() => scene.render());
        }});

        window.addEventListener('resize', () => engine.resize());

        // WebSocket for real-time updates
        const ws = new WebSocket(`ws://${{location.host}}/ws?model={model.ModelId}`);
        ws.onmessage = (e) => {{
            const msg = JSON.parse(e.data);
            if (msg.type === 'comment_added') loadComments();
            if (msg.type === 'model_updated') location.reload();
        }};

        // Comments
        const loadComments = async () => {{
            const res = await fetch('/api/comments/{model.ModelId}');
            const comments = await res.json();
            document.getElementById('comment-count').textContent = comments.length;
            document.getElementById('comments').innerHTML = comments.map(c => `
                <div class=""comment"">
                    <div class=""comment-author"">${{c.author}}</div>
                    <div class=""comment-text"">${{c.content}}</div>
                    <div class=""comment-time"">${{new Date(c.createdAt).toLocaleString()}}</div>
                </div>
            `).join('');
        }};

        const addComment = async () => {{
            const content = document.getElementById('new-comment').value;
            if (!content) return;
            await fetch('/api/comments/{model.ModelId}', {{
                method: 'POST',
                headers: {{ 'Content-Type': 'application/json' }},
                body: JSON.stringify({{ author: 'Viewer', content }})
            }});
            document.getElementById('new-comment').value = '';
            loadComments();
        }};

        loadComments();
    </script>
</body>
</html>";
        }

        #endregion

        #region Helpers

        private async Task WriteJsonAsync(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }

        private async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }

        private string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".gltf" => "model/gltf+json",
                ".glb" => "model/gltf-binary",
                _ => "application/octet-stream"
            };
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _httpListener?.Close();
            _cts?.Dispose();
        }

        #endregion
    }

    #region Data Models

    public class WebViewerConfig
    {
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 8080;
        public string PublicHost { get; set; } = "localhost";
        public string StoragePath { get; set; } = "./models";
        public string AssetsPath { get; set; } = "./assets";
        public int MaxModelSizeMB { get; set; } = 500;
    }

    public class PublishedModel
    {
        public string ModelId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public ModelFormat Format { get; set; }
        public string AccessCode { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string WebModelPath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public string? ShareUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int Version { get; set; } = 1;
        public bool IsPublic { get; set; }
        public bool AllowComments { get; set; } = true;
        public bool AllowMarkups { get; set; } = true;
        public bool RequireAuthentication { get; set; }
        public string? PublishedBy { get; set; }
    }

    public enum ModelFormat
    {
        IFC,
        GLTF,
        GLB,
        OBJ,
        FBX,
        StingBIM
    }

    public class PublishOptions
    {
        public bool IsPublic { get; set; }
        public bool AllowComments { get; set; } = true;
        public bool AllowMarkups { get; set; } = true;
        public bool RequireAuthentication { get; set; }
        public bool GenerateThumbnail { get; set; } = true;
        public DateTime? ExpirationDate { get; set; }
        public string? PublishedBy { get; set; }
    }

    public class ModelComment
    {
        public string CommentId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ViewPosition? Position { get; set; }
        public string? ElementId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }
    }

    public class ViewPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double CameraX { get; set; }
        public double CameraY { get; set; }
        public double CameraZ { get; set; }
    }

    public class ModelMarkup
    {
        public string MarkupId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public MarkupData Data { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class MarkupData
    {
        public string Type { get; set; } = string.Empty; // arrow, circle, text, cloud
        public List<ViewPosition> Points { get; set; } = new();
        public string? Text { get; set; }
        public string Color { get; set; } = "#FF0000";
        public float LineWidth { get; set; } = 2;
    }

    public class ViewerSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public WebSocket WebSocket { get; set; } = null!;
        public string? UserName { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string RemoteEndpoint { get; set; } = string.Empty;
    }

    public class ViewerMessage
    {
        public string Type { get; set; } = string.Empty;
        public object? Payload { get; set; }
    }

    public class AddCommentRequest
    {
        public string Author { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ViewPosition? Position { get; set; }
        public string? ElementId { get; set; }
    }

    public class AddMarkupRequest
    {
        public string Author { get; set; } = string.Empty;
        public MarkupData Data { get; set; } = null!;
    }

    #endregion

    #region Event Args

    public class ModelPublishedEventArgs : EventArgs
    {
        public PublishedModel Model { get; }
        public ModelPublishedEventArgs(PublishedModel model) => Model = model;
    }

    public class CommentAddedEventArgs : EventArgs
    {
        public ModelComment Comment { get; }
        public CommentAddedEventArgs(ModelComment comment) => Comment = comment;
    }

    public class MarkupAddedEventArgs : EventArgs
    {
        public ModelMarkup Markup { get; }
        public MarkupAddedEventArgs(ModelMarkup markup) => Markup = markup;
    }

    public class ViewerConnectedEventArgs : EventArgs
    {
        public ViewerSession Session { get; }
        public ViewerConnectedEventArgs(ViewerSession session) => Session = session;
    }

    #endregion
}
