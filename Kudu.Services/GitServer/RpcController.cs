#region License

// Copyright 2010 Jeremy Skinner (http://www.jeremyskinner.co.uk)
//  
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://github.com/JeremySkinner/git-dot-aspx

// This file was modified from the one found in git-dot-aspx

#endregion

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.GitServer
{
    public class RpcController : ApiController
    {
        private readonly IGitServer _gitServer;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;

        public RpcController(ITracer tracer,
                          IGitServer gitServer,
                          IOperationLock deploymentLock)
        {
            _gitServer = gitServer;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
        }

        [HttpPost]
        public HttpResponseMessage UploadPack()
        {
            using (_tracer.Step("RpcService.UploadPack"))
            {
                var memoryStream = new MemoryStream();

                _gitServer.Upload(GetInputStream(Request), memoryStream);

                return CreateResponse(memoryStream, "application/x-git-{0}-result".With("upload-pack"));
            }
        }

        private Stream GetInputStream(HttpRequestMessage request)
        {
            using (_tracer.Step("RpcService.GetInputStream"))
            {
                if (request.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    return new GZipStream(request.Content.ReadAsStreamAsync().Result, CompressionMode.Decompress);
                }

                return request.Content.ReadAsStreamAsync().Result;
            }
        }

        private HttpResponseMessage CreateResponse(MemoryStream stream, string mediaType)
        {
            _tracer.Trace("Writing {0} bytes", stream.Length);

            HttpContent content = stream.AsContent();

            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            // REVIEW: Why is it that we do not write an empty Content-Type here, like for InfoRefsController?

            var response = new HttpResponseMessage();
            response.Content = content;
            response.WriteNoCache();
            return response;
        }
    }
}
