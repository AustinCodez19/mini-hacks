﻿//  ------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation
//  All rights reserved. 
//  
//  Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this 
//  file except in compliance with the License. You may obtain a copy of the License at 
//  http://www.apache.org/licenses/LICENSE-2.0  
//  
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
//  EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR 
//  CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR 
//  NON-INFRINGEMENT. 
// 
//  See the Apache Version 2.0 License for specific language governing permissions and 
//  limitations under the License.
//  ------------------------------------------------------------------------------------

namespace Amqp
{
    using System;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Amqp.Framing;
    using Amqp.Sasl;

    /// <summary>
    /// The factory to create connections asynchronously.
    /// </summary>
    public class ConnectionFactory : ConnectionFactoryBase
    {
        SslSettings sslSettings;
        SaslSettings saslSettings;

        /// <summary>
        /// Constructor to create a connection factory.
        /// </summary>
        public ConnectionFactory()
            : base()
        {
        }

        /// <summary>
        /// Gets the TLS/SSL settings on the factory.
        /// </summary>
        public SslSettings SSL
        {
            get
            {
                return this.sslSettings ?? (this.sslSettings = new SslSettings());
            }
        }

        /// <summary>
        /// Gets the SASL settings on the factory.
        /// </summary>
        public SaslSettings SASL
        {
            get
            {
                return this.saslSettings ?? (this.saslSettings = new SaslSettings());
            }
        }

        internal SslSettings SslInternal
        {
            get { return this.sslSettings; }
        }

        /// <summary>
        /// Creates a new connection.
        /// </summary>
        /// <param name="address">The address of remote endpoint to connect to.</param>
        /// <returns></returns>
        public Task<Connection> CreateAsync(Address address)
        {
            return this.CreateAsync(address, null, null);
        }

        /// <summary>
        /// Creates a new connection with a custom open frame and a callback to handle remote open frame.
        /// </summary>
        /// <param name="address">The address of remote endpoint to connect to.</param>
        /// <param name="open">If specified, it is sent to open the connection, otherwise an open frame created from the AMQP settings property is sent.</param>
        /// <param name="onOpened">If specified, it is invoked when an open frame is received from the remote peer.</param>
        /// <returns></returns>
        public async Task<Connection> CreateAsync(Address address, Open open, OnOpened onOpened)
        {
            IAsyncTransport transport;
            if (string.Equals(address.Scheme, Address.Amqp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(address.Scheme, Address.Amqps, StringComparison.OrdinalIgnoreCase))
            {
                TcpTransport tcpTransport = new TcpTransport(this.BufferManager);
                await tcpTransport.ConnectAsync(address, this);
                transport = tcpTransport;
            }
#if NETFX
            else if (WebSocketTransport.MatchScheme(address.Scheme))
            {
                WebSocketTransport wsTransport = new WebSocketTransport();
                await wsTransport.ConnectAsync(address);
                transport = wsTransport;
            }
#endif
            else
            {
                throw new NotSupportedException(address.Scheme);
            }

            if (address.User != null)
            {
                SaslPlainProfile profile = new SaslPlainProfile(address.User, address.Password);
                transport = await profile.OpenAsync(address.Host, this.BufferManager, transport);
            }
            else if (this.saslSettings != null && this.saslSettings.Profile != null)
            {
                transport = await this.saslSettings.Profile.OpenAsync(address.Host, this.BufferManager, transport);
            }

            AsyncPump pump = new AsyncPump(this.BufferManager, transport);
            Connection connection = new Connection(this.BufferManager, this.AMQP, address, transport, open, onOpened);
            pump.Start(connection);

            return connection;
        }

        /// <summary>
        /// Contains the TLS/SSL settings for a connection.
        /// </summary>
        public class SslSettings
        {
            internal SslSettings()
            {
                this.Protocols = SslProtocols.Ssl3 | SslProtocols.Tls;
                this.ClientCertificates = new X509CertificateCollection();
            }

            /// <summary>
            /// Client certificates to use for mutual authentication.
            /// </summary>
            public X509CertificateCollection ClientCertificates
            {
                get;
                set;
            }

            /// <summary>
            /// Supported protocols to use.
            /// </summary>
            public SslProtocols Protocols
            {
                get;
                set;
            }

            /// <summary>
            /// Specifies whether certificate revocation should be performed during handshake.
            /// </summary>
            public bool CheckCertificateRevocation
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a certificate validation callback to validate remote certificate.
            /// </summary>
            public RemoteCertificateValidationCallback RemoteCertificateValidationCallback
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a local certificate selection callback to select the certificate which should be used for authentication.
            /// </summary>
            public LocalCertificateSelectionCallback LocalCertificateSelectionCallback
            {
                get;
                set;
            }
        }

        /// <summary>
        /// Contains the SASL settings for a connection.
        /// </summary>
        public class SaslSettings
        {
            /// <summary>
            /// The SASL profile to use for SASL negotiation.
            /// </summary>
            public SaslProfile Profile
            {
                get;
                set;
            }
        }
    }
}
