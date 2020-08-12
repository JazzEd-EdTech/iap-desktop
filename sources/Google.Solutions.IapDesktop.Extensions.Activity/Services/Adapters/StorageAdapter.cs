﻿//
// Copyright 2019 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Apis.Auth.OAuth2;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Util;
using Google.Solutions.IapDesktop.Application;
using Google.Solutions.IapDesktop.Application.ObjectModel;
using Google.Solutions.IapDesktop.Application.Services.Adapters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Google.Solutions.IapDesktop.Extensions.Activity.Services.Adapters
{
    public interface IStorageAdapter
    {
        Task<Stream> DownloadObjectToMemoryAsync(
            StorageObjectLocator locator,
            CancellationToken cancellationToken);

        Task<IEnumerable<Bucket>> ListBucketsAsync(
            string projectId,
            CancellationToken cancellationToken);

        Task<IEnumerable<GcsObject>> ListObjectsAsync(
            string bucket,
            string prefix,
            CancellationToken cancellationToken);
    }

    [Service(typeof(IStorageAdapter))]
    public class StorageAdapter : IStorageAdapter
    {
        private readonly StorageService service;

        public StorageAdapter(ICredential credential)
        {
            this.service = new StorageService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = Globals.UserAgent.ToApplicationName()
            });
        }
        public StorageAdapter(IServiceProvider serviceProvider)
            : this(serviceProvider.GetService<IAuthorizationAdapter>().Authorization.Credential)
        {
        }

        //---------------------------------------------------------------------
        // IStorageAdapter.
        //---------------------------------------------------------------------

        public async Task<IEnumerable<Bucket>> ListBucketsAsync(
            string projectId,
            CancellationToken cancellationToken)
        {
            using (TraceSources.IapDesktop.TraceMethod().WithParameters(projectId))
            {
                try
                {
                    var buckets = await this.service.Buckets.List(projectId)
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return buckets.Items.EnsureNotNull();
                }
                catch (GoogleApiException e)
                    when (e.Error != null && (e.Error.Code == 403))
                {
                    throw new ResourceAccessDeniedException(
                        $"Permission to list buckets in project {projectId} has been denied", e);
                }
            }
        }

        public async Task<IEnumerable<GcsObject>> ListObjectsAsync(
            string bucket,
            string prefix,
            CancellationToken cancellationToken)
        {
            using (TraceSources.IapDesktop.TraceMethod().WithParameters(bucket))
            {
                try
                {
                    var request = this.service.Objects.List(bucket);
                    request.Prefix = prefix;

                    var objects = await new PageStreamer<
                        GcsObject,
                        ObjectsResource.ListRequest,
                        Objects,
                        string>(
                            (req, token) => req.PageToken = token,
                            response => response.NextPageToken,
                            response => response.Items)
                        .FetchAllAsync(
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);

                    TraceSources.IapDesktop.TraceVerbose("Found {0} instances", objects.Count());

                    return objects;
                }
                catch (GoogleApiException e) 
                    when (e.Error != null && (e.Error.Code == 403 || e.Error.Code == 404))
                {
                    throw new ResourceAccessDeniedException(
                        $"Access to storage bucket {bucket} has been denied", e);
                }
            }
        }

        public async Task<Stream> DownloadObjectToMemoryAsync(
            StorageObjectLocator locator,
            CancellationToken cancellationToken)
        {
            using (TraceSources.IapDesktop.TraceMethod().WithParameters(locator))
            {
                try
                {
                    var buffer = new MemoryStream();
                    var result = await this.service.Objects
                        .Get(locator.Bucket, locator.ObjectName)
                        .DownloadAsync(buffer)
                        .ConfigureAwait(false);

                    if (result.Exception != null)
                    {
                        throw result.Exception;
                    }

                    buffer.Seek(0, SeekOrigin.Begin);
                    return buffer;
                }
                catch (GoogleApiException e) 
                    when ((e.Error != null && (e.Error.Code == 403 || e.Error.Code == 404)) ||
                          e.Message == "Not Found" ||
                          e.Message.Contains("storage.objects.get access"))
                {
                    throw new ResourceAccessDeniedException(
                        $"Access to storage bucket {locator.Bucket} has been denied", e);
                }
            }
        }
    }
}