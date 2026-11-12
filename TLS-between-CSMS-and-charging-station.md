# TLS between a CSMS and a charging station, with certificates of this PKI

How to give a CSMS (the TLS server) and a charging station (the TLS client)
one certificate each, so that the station connects with OCPP security
profile 3: TLS with a client certificate.

The programs are [PKICLI](https://github.com/OpenChargingCloud/PKICLI),
[CSMSCLI](https://github.com/OpenChargingCloud/CSMSCLI) and
[ChargingStationCLI](https://github.com/OpenChargingCloud/ChargingStationCLI).
In the examples the CSMS is reached as `csms.example.org` and the station is
called `cs001`.

> Played through on Windows on 2026-11-12, all three programs on one machine:
> the station connected, the CSMS said "The charging station 'cs001' signed in
> ... with a certificate (security profile 3)", and its `BootNotification`
> arrived. Not before the root was in the trust store of Windows - see
> [The root on both machines](#the-root-on-both-machines) - and not yet tried
> with the CSMS and the station on two machines.


## The three CAs

```
Root CA
├── Server Sub-CA  ──  csms.example.org   (server certificate, on the CSMS)
└── Client Sub-CA  ──  cs001              (client certificate, on the station)
```

No private key is ever copied. The CSMS and the station each make their own
key and hand out a signing request (CSR); only certificates travel.


## 1. The PKI: make the CAs

On the **PKI** page of the PKI's web interface:

1. **New root CA** - common name `Root CA`.
2. **New sub-CA** below it - common name `Server Sub-CA`.
3. **New sub-CA** below it - common name `Client Sub-CA`.

Then download three files, each from the details of its certificate ("PEM"):

| File | What it is |
|---|---|
| `root.pem` | the Root CA |
| `server-sub.pem` | the Server Sub-CA |
| `client-sub.pem` | the Client Sub-CA |

At the command line, `pki` lists them and `pkiShow Root CA` shows one.


## 2. The CSMS: its server certificate

1. **CSMS**, page **Charging stations**: fill in **reachable as** with every
   name the stations dial - `csms.example.org` - and the address and port.
   Without it the CSMS makes no signing request. Address and port take effect
   at the next start of the CSMS.
2. **CSMS**, page **Server certificates**, tab *Keys and requests*: make a key
   and copy its signing request. The names of step 1 are in it.
3. **PKI**, form **Sign a CSR**: paste the request, issuer `Server Sub-CA`,
   profile `server`, and take the alternative names of the request over.
   Download the certificate as `csms.pem`.
4. **CSMS**, page **Server certificates**, tab *Upload*: paste `csms.pem` and
   `server-sub.pem` together. The CSMS puts each under the key it belongs to
   and says that it presents the certificate with one intermediate.
5. **CSMS**, page **Charging stations**: allow security profile 3 - alone, if
   no station is to get in with a password - and switch the server on. Profiles
   2 and 3 are refused while there is no certificate, which is why this comes
   last.

The names of step 1 must hold the host name in the station's `wss://` address.
A certificate for another name is refused by the station.


## 3. The CSMS: which stations it lets in

1. **CSMS**, page **Accepted chains**: add `root.pem` and `client-sub.pem`
   as one chain. This list alone decides whose client certificates are
   believed - not the trust store of the operating system. An empty list means
   nobody gets in with a certificate.
2. **CSMS**, page **Logins and groups**: a login `cs001` that may sign in with
   a `certificate`, in a group that allows security profile 3.

The station is recognised by the **common name** of its certificate, which
must be the name of its login: `cs001`.


## 4. The charging station: its client certificate

1. **Station**, page `/configuration/client-keys`: make a key with the subject
   `CN=cs001` and copy its signing request. Take ECDSA P-256; the station
   offers thirteen kinds of key, and whether the PKI signs a request of every
   one of them is untested.
2. **PKI**, form **Sign a CSR**: paste the request, issuer `Client Sub-CA`,
   profile `client`. Download the certificate as `cs001.pem`.
3. **Station**, page `/configuration/client-keys`: bring `cs001.pem` back to
   the key it was made for. The station warns that the chain does not verify
   on it; for a private CA that is as it should be.


## 5. The charging station: dialling the CSMS

On the **Connections** page (`/configuration/connections`): a connection to
`wss://csms.example.org:<port>/cs001` - the address the CSMS's **Charging
stations** page gives, with the name of the station's login as its last
segment - as OCPP 2.1, that proves itself with the client key of step 4. A
connection is dialled at the station's next start. The page's test says step by
step what happened.


## The root on both machines

The station believes the certificate of the CSMS by the trust store of the
machine it runs on. So `root.pem` goes into that store - on Windows:

```
Import-Certificate -FilePath root.pem -CertStoreLocation Cert:\CurrentUser\Root
```

**On Windows the CSMS sent its own certificate alone**, without the Server
Sub-CA, although it said it presents one intermediate - before the root was in
the store of Windows and after it, also after a restart. A client that knows
only the root then finds the chain incomplete (`PartialChain`). The station got
through all the same because it ran on the same machine: when the CSMS loads
its certificate, .NET puts the Server Sub-CA into the user's store of
intermediate authorities, where the station's Windows found it. With the
station on another machine, expect to need `server-sub.pem` in that machine's
store of intermediate authorities as well, until the CSMS sends it:

```
Import-Certificate -FilePath server-sub.pem -CertStoreLocation Cert:\CurrentUser\CA
```

What was found on the way, and is not as one would expect:

- The station's own certificate store takes `root.pem` as a TLS root "for a
  back end", but the connection to the CSMS was not checked against it - only
  against the trust store of Windows.
- The station refuses a sub-CA as a TLS root, rightly: it has to come from the
  server.
- A key pin on the connection, which would need no root, was not taken by the
  station binary this was tried with.
- The station's connection test shows that the handshake failed, but not why.


## What goes where

| | Copied to the CSMS | Copied to the charging station |
|---|---|---|
| `csms.pem` (its own certificate) | yes - Server certificates | no |
| `server-sub.pem` | yes - Server certificates, so that the CSMS sends it along | no |
| `cs001.pem` (the station's certificate) | no | yes - client keys |
| `client-sub.pem` | yes - Accepted chains | no; the CSMS keeps it |
| `root.pem` | yes - Accepted chains | yes - the machine's trust store |
| `server-sub.pem`, again | | on Windows, where the CSMS does not send it: the machine's store of intermediate authorities |
| any private key | never | never |

The station does not need the Server Sub-CA: the CSMS sends it in the
handshake. The CSMS does not need the station's certificate: it believes
whatever leads to an accepted chain and names a login.


## If it does not connect

| What is said | Where to look |
|---|---|
| the station is "not dialled" | it has no certificate for the key the connection names |
| the station's test ends with a 400 that no server sent, and the CSMS logs that the station's certificate was accepted, but no sign-in | the station did not believe the CSMS: the root is missing in the trust store of the station's machine, the Server Sub-CA did not arrive, or the certificate is not for the name dialled |
| the CSMS turns the station away | no accepted chain leads to its certificate, its common name is no login, or the login's group does not allow profile 3 |

`pkiShow cs001` at the PKI's command line says who signed a certificate and
whether it is revoked; `pkiRevoke cs001 superseded` revokes it.
