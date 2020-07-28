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

using Google.Solutions.Common.Locator;
using Google.Solutions.Common.Test;
using Google.Solutions.Common.Test.Integration;
using Google.Solutions.IapTunneling.Iap;
using Google.Solutions.IapTunneling.Net;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Google.Solutions.IapTunneling.Test.Iap
{
    [TestFixture]
    [Category("IntegrationTest")]
    [Category("IAP")]
    public class TestSshRelayProber : FixtureBase
    {
        [Test]
        public async Task WhenProjectDoesntExist_ThenProbeFailsWithUnauthorizedException(
            [Credential(Role = PredefinedRole.IapTunnelUser)] CredentialRequest credential)
        {
            using (var stream = new SshRelayStream(
                new IapTunnelingEndpoint(
                    await credential.GetCredentialAsync(),
                    new InstanceLocator(
                        "invalid",
                        TestProject.Zone,
                        "invalid"),
                    80,
                    IapTunnelingEndpoint.DefaultNetworkInterface,
                    TestProject.UserAgent)))
            {
                AssertEx.ThrowsAggregateException<UnauthorizedException>(() =>
                    stream.TestConnectionAsync(TimeSpan.FromSeconds(10)).Wait());
            }
        }

        [Test]
        public async Task WhenZoneDoesntExist_ThenProbeFailsWithUnauthorizedException(
            [Credential(Role = PredefinedRole.IapTunnelUser)] CredentialRequest credential)
        {
            using (var stream = new SshRelayStream(
               new IapTunnelingEndpoint(
                    await credential.GetCredentialAsync(),
                    new InstanceLocator(
                        TestProject.ProjectId,
                        "invalid",
                        "invalid"),
                    80,
                    IapTunnelingEndpoint.DefaultNetworkInterface,
                    TestProject.UserAgent)))
            {
                AssertEx.ThrowsAggregateException<UnauthorizedException>(() =>
                    stream.TestConnectionAsync(TimeSpan.FromSeconds(10)).Wait());
            }
        }

        [Test]
        public async Task WhenInstanceDoesntExist_ThenProbeFailsWithUnauthorizedException(
            [Credential(Role = PredefinedRole.IapTunnelUser)] CredentialRequest credential)
        {
            using (var stream = new SshRelayStream(
                new IapTunnelingEndpoint(
                    await credential.GetCredentialAsync(),
                    new InstanceLocator(
                        TestProject.ProjectId,
                        TestProject.Zone,
                        "invalid"),
                    80,
                    IapTunnelingEndpoint.DefaultNetworkInterface,
                    TestProject.UserAgent)))
            {
                AssertEx.ThrowsAggregateException<UnauthorizedException>(() =>
                    stream.TestConnectionAsync(TimeSpan.FromSeconds(10)).Wait());
            }
        }

        [Test]
        public async Task WhenInstanceExistsAndIsListening_ThenProbeSucceeds(
             [WindowsInstance] InstanceRequest testInstance,
             [Credential(Role = PredefinedRole.IapTunnelUser)] CredentialRequest credential)
        {
            await testInstance.AwaitReady();

            using (var stream = new SshRelayStream(
                new IapTunnelingEndpoint(
                    await credential.GetCredentialAsync(),
                    testInstance.Locator,
                    3389,
                    IapTunnelingEndpoint.DefaultNetworkInterface,
                    TestProject.UserAgent)))
            {
                await stream.TestConnectionAsync(TimeSpan.FromSeconds(10));
            }
        }

        [Test]
        public async Task WhenInstanceExistsButNotListening_ThenProbeFailsWithNetworkStreamClosedException(
             [WindowsInstance] InstanceRequest testInstance,
             [Credential(Role = PredefinedRole.IapTunnelUser)] CredentialRequest credential)
        {
            await testInstance.AwaitReady();

            using (var stream = new SshRelayStream(
                new IapTunnelingEndpoint(
                    await credential.GetCredentialAsync(),
                    testInstance.Locator,
                    22,
                    IapTunnelingEndpoint.DefaultNetworkInterface,
                    TestProject.UserAgent)))
            {
                AssertEx.ThrowsAggregateException<NetworkStreamClosedException>(() =>
                    stream.TestConnectionAsync(TimeSpan.FromSeconds(5)).Wait());
            }
        }
    }
}
