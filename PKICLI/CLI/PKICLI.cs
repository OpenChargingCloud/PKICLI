/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of PKICLI <https://github.com/OpenChargingCloud/PKICLI>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Reflection;

using org.GraphDefined.Vanaheimr.CLI;

using cloud.charging.open.protocols.WWCP.Node.CommandLine;

#endregion

namespace cloud.charging.open.PKI.CommandLine
{

    /// <summary>
    /// The command line of a running PKI.
    /// </summary>
    /// <remarks>
    /// Everything a command needs is reachable from here, which is why every
    /// command takes one of these: the PKI itself, and through it its
    /// configuration, its log and everything the JSON API can do. A command is
    /// a third way of asking for the same thing, beside the web interface and
    /// the switches at a start - never an implementation of its own.
    ///
    /// Commands are not listed anywhere. The commands every node has - syncNTS
    /// among them - are the node's, and NodeCLI, which this derives from,
    /// finds them in WWCP_Node. The constructor registers this type as well,
    /// so that this assembly is walked for anything that implements
    /// ICLICommand and can be built from a PKICLI: a command of the
    /// PKI's own is a new file and nothing else - 'pki' among them.
    /// </remarks>
    public class PKICLI : NodeCLI
    {

        #region Properties

        /// <summary>
        /// The PKI these commands are about.
        /// </summary>
        public PKI PKI { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the command line of the given PKI.
        /// </summary>
        /// <param name="PKI">The running PKI.</param>
        /// <param name="AssembliesWithCLICommands">Further assemblies to search for commands. This one and WWCP_Node are searched either way.</param>
        public PKICLI(PKI                PKI,
                          params Assembly[]  AssembliesWithCLICommands)

            : base(PKI, AssembliesWithCLICommands)

        {

            this.PKI = PKI;

            RegisterCLIType(typeof(PKICLI));

        }

        /// <summary>
        /// Create the command line of the given PKI on the given terminal,
        /// for the given caller - a session over SSH.
        /// </summary>
        /// <param name="PKI">The running PKI.</param>
        /// <param name="Terminal">What the command line is typed at and written on.</param>
        /// <param name="Caller">Who is typing at it.</param>
        /// <param name="AssembliesWithCLICommands">Further assemblies to search for commands. This one and the node's are searched either way.</param>
        public PKICLI(PKI                PKI,
                          ICLITerminal       Terminal,
                          CLICaller          Caller,
                          params Assembly[]  AssembliesWithCLICommands)

            : base(PKI, Terminal, Caller, AssembliesWithCLICommands)

        {

            this.PKI = PKI;

            RegisterCLIType(typeof(PKICLI));

        }

        #endregion


        #region (protected override) GetPrompt()

        /// <summary>
        /// Which port this PKI answers on, because a machine that is one of
        /// several on a bench should say which one it is before it asks for a
        /// command.
        /// </summary>
        protected override String GetPrompt()

            => $"pki:{PKI.HTTPPort}> ";

        #endregion

    }

}
