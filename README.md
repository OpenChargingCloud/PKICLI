# PKICLI

[![CI](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/ci.yml/badge.svg)](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/ci.yml)
[![Nightly](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/nightly.yml/badge.svg)](https://github.com/OpenChargingCloud/PKICLI/actions/workflows/nightly.yml)

One public key infrastructure, with a web interface and a prompt, until
'quit' or Ctrl+C.

The PKI makes the X.509 certificates every other program of this family is
known by: root CAs, sub-CAs below them, certificates for servers - a CSMS, a
local controller - and for clients - a charging station towards its CSMS -
and certificates for the key of a certificate signing request, such as a
charging station sends when it wants a new OCPP client certificate.

It is a sibling of [EVCLI](https://github.com/OpenChargingCloud/EVCLI),
[GatewayCLI](https://github.com/OpenChargingCloud/GatewayCLI) and the others,
and is built the same way: a [Hermod](https://github.com/Vanaheimr/Hermod)
HTTP server carrying a JSON API and one Server-Sent Events stream, and a web
interface built by npm and embedded into the assembly. What every program of
the family has - the sign-in, the name servers (DNS), the time servers (NTS),
the certificates they are held to, the SSH server and the log - is
[WWCP_Node](https://github.com/OpenChargingCloud/WWCP_Node); what is the PKI's
own is in [PKI](https://github.com/OpenChargingCloud/PKI).


### Getting it

The libraries it is built from are submodules, so they have to come along:

```
git clone --recurse-submodules https://github.com/OpenChargingCloud/PKICLI.git
```

If you already cloned it without them:

```
git submodule update --init --recursive
```

**On Windows**, turn long paths on first: `git config --global core.longpaths true`.


### Building and running it

.NET 10 and Node 22 or newer. The build runs `npm ci` and `npm run build` for
the web interface itself whenever its inputs changed:

```
dotnet build PKICLI.slnx
./run.sh
```

It listens on http://127.0.0.1:2360/ - `--any` for every address, `--port`
for another one, `--help` for every switch. At the first start it makes one
account, `root`, and shows its password once, on the console.

**`--any` puts the sign-in on the network** - the password, the API keys and
the session cookie, and with them the private keys `GET
/v1/pki/certificates/{id}/key` hands out - in the clear, unless the PKI speaks
HTTPS itself or a proxy in front of it does and is named as trusted. Either
is said by switches, `--https`, `--https-cert <file>`, `--https-key <file>`,
`--redirect-port <number>` and `--trusted-proxy <address>`, or in the `web`
section of the configuration file. Behind nginx:

```json
"web": { "trustedProxies": [ "127.0.0.1", "::1" ] }
```

With PEM files of its own (a certificate this PKI issued itself will do) and
no proxy:

```json
"web": { "tls": { "certificate": { "fullchain":  "/etc/pki/pki.example.org/fullchain.pem",
                                   "privateKey": "/etc/pki/pki.example.org/privkey.pem" },
                  "redirectPort": 80 } }
```

Neither said, the PKI warns in its log, below its banner and above every page
of its web interface to its administrators.
[WWCP_Node's README](https://github.com/OpenChargingCloud/WWCP_Node) says the
rest.

Beside the solution it writes `configuration.json`, `accounts/`, `logs/`,
`certificates/` - what the PKI itself believes, the roots of its time servers
say - and **`pki/`**, what it makes: one file per certificate and one per
private key, named by the SHA-256 fingerprint, `index.json` beside them,
`pki/transparency/`, its certificate transparency log, and `pki/revocation/`,
what it revoked. All of these are in `.gitignore`.

**A private key may have a password, and need not.** Without one it is kept
unencrypted, and whoever can read `pki/` can sign as every CA whose key has
none - the log says how many there are at every start. With one, it is an
`ENCRYPTED PRIVATE KEY` - PKCS#8, AES-256-CBC, its key derived from the
password by PBKDF2 with SHA-256 and 600,000 iterations - which openssl opens as
it opens any other. The password is kept nowhere: a CA whose key has one signs
only where it is given again, and the key of a server's or a client's
certificate is downloaded encrypted, for whoever has it to open with the
password. A password has 8 characters at least; one that is forgotten makes
its key useless. It is given where a key is made, and given, changed or taken
away later in the details of its certificate.

**Every certificate the PKI signs is in its certificate transparency log**,
after RFC 6962: in the order it was signed, in a Merkle tree whose head the log
signs with a key of its own, an ECDSA key of P-256 in
`pki/transparency/log-key.pem`. A certificate is written to the log before it
is kept anywhere else, so there is none the log does not have, and it stays in
the log when it is deleted from the PKI - that is the point of one. Whoever
keeps a signed tree head can have the log prove that a certificate is in it,
and that the log is the one they saw before, grown and nothing else. A PKI from
before its log has every certificate it keeps taken in at the first start, CAs
first.

`entries.jsonl` is only ever appended to, and `sth.json` keeps the newest tree
head. At every start the log checks every signature in it and its tree against
that head; where they do not hold - an entry changed or taken out - the log is
**broken**, signs no tree head, and the PKI signs nothing until it is repaired,
rather than sign what the log does not have. Half an entry at its end, written
when the PKI stopped, is cut off.

**A server's or a client's certificate carries its signed certificate timestamp
in itself**, as RFC 6962, section 3.1, has it: the CA first signs a
precertificate - the certificate to be, with the critical poison extension, so
that nothing believes it - the log signs the timestamp of that, and the
certificate is signed with the timestamp where the poison was, and is otherwise
the same, byte for byte; the log checks that before it writes its entry. A TLS
client that checks certificate transparency - `openssl s_client -ct`, a browser
with the log among those it knows - checks it from the certificate alone. The
log keeps the precertificate as a `precert_entry`, and `get-entries` hands it
out with its `PrecertChainEntry`. CAs carry no timestamp, nor does a certificate
of ISO 15118, whose profiles have no room for one: they are in the log as
themselves.

**A certificate is revoked for good**, with a reason of RFC 5280 - not a root,
which no CA above it can say anything of; a root is believed no more by being
taken out of every trust store. Revoked, it is in the **CRL** of its CA, signed
anew at once, and the CA's **OCSP responder** (RFC 6960) answers `revoked` for
it - also after it is deleted from the PKI, since deleting is not revoking.
`pki/revocation/revoked.json` lists every revocation, and per CA `<id>.crl` is
its newest CRL and `<id>.ocsp.crt` and `<id>.ocsp.key` its responder.

A CRL is signed with the key of its CA. Where that key has no password, the
PKI signs it anew by itself, for 7 days at a time, before half of that is over;
where it has one, the CRL is signed whenever the password is given - the CA
signs or revokes a certificate, or its CRL is signed by hand - for 30 days or
as long as is asked, and the log says when one runs out within 3 days. Revoking
below such a CA asks for its password, so that its CRL says it at once.

An OCSP response is never signed with the key of a CA, but by a responder the
CA signed for it, with the extended key usage of OCSP signing and
`id-pkix-ocsp-nocheck`, whose key has no password: it answers at any hour,
whatever the CA's key has, and signs nothing but OCSP responses. It is made, and
made anew 90 days before its year runs out, wherever the CA's key is open, and
is in the transparency log as everything the PKI signs. It says `good` for a
certificate the CA signed, `revoked` for one it revoked and `unknown` for a
serial number it never signed - the transparency log says which were.

Where whoever checks a certificate finds its CRL and the responder is written
into it - as its CRL distribution point and its authority information access,
beside the address of its CA's certificate - once the PKI is told the address
it is reached at from outside: `"pki": { "publicURL": "http://pki.example.org" }`
in `configuration.json`, or on the Revocation tab. A certificate made before
names none. http rather than https serves most of whoever checks: a CRL and an
OCSP response are signed, and checking the certificate of an https server
would need the very answer being fetched.

**Charging stations enroll themselves over EST**, RFC 7030, at
`/.well-known/est` of the PKI's HTTP server: `cacerts` hands out the CA that
signs and those above it, `simpleenroll` signs a CSR - a PKCS#10 in Base64, as
`application/pkcs10` - and answers with the certificate, both as a certs-only
PKCS#7 in Base64; `simplereenroll` renews one, and `csrattrs` asks nothing in
particular - but for ISO 15118, below. What a CSR is signed as is what the PKI was told, whatever the CSR
asks for - its subject and its names are taken, nothing else:

```json
"pki": {
  "est": {
    "issuer": "<fingerprint of a CA>", "profile": "client", "validDays": 365,
    "labels": { "csms": { "issuer": "<fingerprint of a CA>", "profile": "server", "validDays": 365 },
                "secc": { "issuer": "<fingerprint of a CPO sub-CA>", "v2g": "CPO", "validDays": 365 } },
    "tlsClientCertificates": true,
    "serverKeyGeneration": false
  }
}
```

A request at `/.well-known/est/simpleenroll` is signed as the section itself
says, one at `/.well-known/est/csms/simpleenroll` as its label `csms` says; the
EST tab of the web interface says the same. Who enrolls signs in with an API key
or HTTP Basic, as an account that may sign certificates of servers and clients
- a backend of charging stations in the `registrar` role, say. Who renews may
instead show the certificate it renews as its TLS client certificate, where the
PKI speaks HTTPS of its own and `tlsClientCertificates` has it ask for one: a
certificate of this PKI, valid and not revoked, renews itself for a new key and
the same subject and names, nobody signed in. Asked for and never demanded - a
browser that has certificates of its own may ask which one to show. Behind a
proxy that ends TLS, a station renews with an API key. The CA signs without
anybody there, so a CA whose key has a password signs nothing over EST.

With `serverKeyGeneration`, `serverkeygen` makes the key for a station that
cannot make one of its own: its CSR carries the subject and the names, and its
key and its signature are not looked at, as RFC 7030, section 4.4.1, has it.
The PKI makes a key of the kind the CSR was signed with - where it makes those,
ECC P-256 otherwise - and answers `multipart/mixed`: the key as PKCS#8 in
Base64, unencrypted, and the certificate as at `simpleenroll`. Over HTTPS only
- the PKI's own, or that of a trusted proxy in front - since TLS is all that
protects the key on its way; over plain HTTP it is refused. The key stays in the
PKI, as every key it makes does. Who may ask is who may enroll, or who renews
with the certificate it renews.

**A server's or a client's certificate goes out with its key as PKCS#12**: the
certificate, its private key and the sub-CAs above it - not the root - in one
file, encrypted with a password of its own of 8 characters at least, with
AES-256 and PBKDF2 with SHA-256 - or with Triple-DES and SHA-1, for older
devices that open nothing else. Its PBKDF2 has the 2,000 iterations of .NET's
export rather than the 600,000 a key here is kept with, since Windows and .NET
themselves open no PKCS#12 file of that many: the password of the file is to
be a long one.

**The certificates of ISO 15118-2 - Plug & Charge** - are made as its profiles
have them. A root CA is made as one of them, of the domain `V2G` - the root of
charging station operators - `MO` or `OEM`; below it the PKI makes certificates
of ISO 15118 only, and none of them below a CA of X.509 of its own:

| Certificate | Profile | Domain | Key usage, critical |
|---|---|---|---|
| V2G, MO or OEM root CA | root CA | `V2G`, `MO`, `OEM` | keyCertSign, cRLSign |
| sub-CA | sub-CA | `V2G` (a CPO's), `MO`, `OEM`, `CPS` | keyCertSign, cRLSign |
| SECC certificate | server | `CPO` | digitalSignature, keyAgreement |
| contract certificate | client | `MO` | digitalSignature, nonRepudiation, keyEncipherment, keyAgreement |
| OEM provisioning certificate | client | `OEM` | digitalSignature, keyAgreement |
| CPS certificate | client | `CPS` | digitalSignature |

Every key is ECC P-256 and every signature ECDSA with SHA-256; the domain is
the DomainComponent, first in the subject - put there where a CSR names none,
and a CSR of another domain is not signed. A root has at most two sub-CAs below
it, the first of path length 1 and the second of 0. A certificate carries basic
constraints and its key usage, both critical, its key identifiers and - below a
root, where the PKI was told its address - its CRL and its OCSP responder, but
not where its CA is found, and nothing else: no extended key usage, no
alternative names, no signed certificate timestamp. ISO 15118-2 carries a
certificate in 800 bytes at most, and one that would be larger is not made. A
sub-CA below a V2G root may be of any domain but `CPO`; below any other CA it is
of its CA's domain. A SECC certificate stands below a CA of `V2G`, every other
certificate below one of its own domain.

**A charging station enrolls its SECC certificate over EST**, as it does its
other certificates: below a CA of ISO 15118, EST signs what that CA signs - a
SECC certificate below a sub-CA of `V2G`, a contract certificate below one of
`MO` - and says so in its settings where it is not told, as `v2g`; below a CA
of X.509, nothing of ISO 15118, and the other way round. `csrattrs` asks the
station for a key of secp256r1 and a signature of ECDSA with SHA-256, as RFC
7030, section 4.5.2, has it; a CSR for any other key is refused. The subject of
the CSR is given its DomainComponent where it names none - and refused where it
names another - and the names it asks for are not taken. A SECC certificate
renews itself as every certificate does, and `serverkeygen` makes it a key of
secp256r1, whatever the CSR was signed with. That is where a CSMS sends the CSR
of a station's `SignCertificateRequest` of OCPP 2.0.1 for its
`V2GCertificate`, at the label of its CPO sub-CA.


### The web interface

| | |
|---|---|
| **PKI** (`/pki`) | every certificate of the PKI, filtered by a text in its common name and by its profile; the details of one - subject, chain, validity, key, fingerprint, its revocation, and of a CA its CRL and OCSP responder - with its PEM to copy or download, its chain, its CRL, the private key of a server's or client's certificate made here and the certificate with it as PKCS#12, the password of its key, the CRL of a CA signed anew, Revoke and Delete; and the forms: **New root CA**, **New sub-CA**, **New certificate** (server or client, with a key made here) and **Sign a CSR**; and the **Transparency log**: its address for a monitor, its id, its newest tree head and public key, and every entry, the newest first, a deleted certificate among them; **Revocation**: the address the PKI is reached at, the CRL and the responder of each CA, and every certificate revoked; and **EST**: what it signs without a label and at each label, whether it asks for a TLS client certificate, and whether it makes keys at `serverkeygen`. A root CA may be made as one of ISO 15118; below a CA of it, the forms make certificates of ISO 15118 - a sub-CA of the domain chosen, a SECC, contract, OEM provisioning or CPS certificate as its CA's domain has it - and ask neither for a key nor for other names, and EST below one says what it signs in place of server or client. The details of a certificate say whether it carries its timestamp, and the transparency log which entries are precertificates |
| **Configuration** | what the PKI is running, how many certificates of each profile it keeps, and below it the **DNS client**, the **NTS client**, the **SSH server**, the **Trusted certificates** and **Identities** of the node itself |
| **Logs** | the log as it happens |

Deleting is hard and final: the files and the key are gone. A CA that signed
certificates still in the PKI is deleted only together with everything below
it, and only after a question that says how many. Deleting is not revoking -
whoever believes the root of a certificate that went out goes on believing it,
until it is revoked.


### The JSON API

What every node answers is WWCP_Node's. The PKI adds, below `/api/v1/pki`:

| | |
|---|---|
| `GET certificates?commonName=&profile=` | every certificate, or those whose common name holds a text and of a profile (`rootCA`, `subCA`, `server`, `client`) |
| `GET certificates/{id}` | one, with its PEM, its chain without the root, and how many it signed |
| `GET certificates/{id}/key` | the private key of a server's or client's certificate made here, as it is kept - encrypted where it has a password, and `encrypted` says so - never a CA's |
| `POST certificates/{id}/key/password` | `{ password, newPassword }`: give its key a password, change it, or take it away with `null` |
| `POST certificates/{id}/pkcs12` | `{ keyPassword, pkcs12Password, legacy }`: the certificate, its key and the sub-CAs above it as PKCS#12, in Base64 - never a CA's |
| `POST certificates/{id}/revoke` | `{ reason, issuerKeyPassword }`: revoke it for good, for `unspecified`, `keyCompromise`, `caCompromise`, `affiliationChanged`, `superseded`, `cessationOfOperation` or `privilegeWithdrawn` |
| `POST certificates/{id}/crl` | `{ keyPassword, validDays }`: sign the CRL of a CA anew |
| `GET revocation`, `PUT revocation` | the address the PKI is reached at, `{ publicURL }`, the CRL and the responder of each CA, and every certificate revoked |
| `GET crl/{id}.crl` | the newest CRL of a CA, as DER - to anybody |
| `GET ca/{id}.crt` | the certificate of a CA, as DER - to anybody |
| `POST ocsp`, `GET ocsp/{request}` | the OCSP responder of RFC 6960 - to anybody |
| `GET est`, `PUT est` | what EST signs: `{ issuer, profile, validDays, v2g, labels, tlsClientCertificates, serverKeyGeneration }`, each label `{ issuer, profile, validDays, v2g }` |
| `DELETE certificates/{id}?withIssued=true` | delete it, with everything it signed |
| `POST rootCAs`, `POST subCAs` | `{ subject, keyAlgorithm, validDays, pathLength, keyPassword, v2g }`, a sub-CA with its `issuer` and `issuerKeyPassword` |
| `POST certificates` | `{ issuer, issuerKeyPassword, profile, subject, keyAlgorithm, validDays, subjectAlternativeNames, keyPassword, v2g }` |
| `POST csr`, `POST csr/inspect` | `{ issuer, issuerKeyPassword, profile, csr, validDays, subjectAlternativeNames, takeSubjectAlternativeNames, v2g }`, and what a CSR says before that |
| `GET transparency` | the transparency log: its id, its public key, its size, its newest signed tree head, and what is wrong with it |
| `GET transparency/entries?start=&end=` | its entries, each with what its certificate is, whether it is a precertificate and whether the PKI still has it |
| `GET transparency/ct/v1/...` | what RFC 6962, section 4, has a log answer: `get-sth`, `get-sth-consistency`, `get-proof-by-hash`, `get-entries`, `get-roots` and `get-entry-and-proof` |

And EST itself at `/.well-known/est[/<label>]/`: `GET cacerts`, `POST
simpleenroll`, `POST simplereenroll`, `POST serverkeygen` and `GET csrattrs`.

`v2g` makes a certificate of ISO 15118, of the domain it names - `V2G`, `CPO`,
`MO`, `OEM` or `CPS` - with ECC P-256 where no key is asked for; a certificate
of a server or a client takes its profile from it where none is given, `server`
for `CPO` and `client` for every other. Every certificate says its `v2g`, or
`null`.

A `subject` is `{ commonName, organization, organizationalUnit, country,
state, locality }`, of which only the common name is needed. Key algorithms are
`ecc-p256`, `ecc-p384`, `ecc-p521`, `rsa-2048`, `rsa-3072` and `rsa-4096`.
`keyPassword` and `issuerKeyPassword` are optional: left out, `null` or empty,
the new key has no password, and the CA that signs is taken to have none. A
password is said back by no answer and written to no log.

A CSR is signed for its key and as what was chosen: its subject is taken over,
its alternative names where that is asked for, and nothing else it asks for -
a CSR asking to be a CA is still signed as a client's certificate. A program
that sends CSRs on its own - a backend for its charging stations - signs in
with an API key of an account in the `registrar` role.

The details of a certificate say where it is in the transparency log, with its
signed certificate timestamp, and whether the certificate carries it itself. A monitor of certificate transparency is pointed
at `https://<pki>/api/v1/pki/transparency`, the log's public key and its
maximum merge delay, which is none: a certificate is in the tree as soon as it
is signed. Unlike the logs of the web, this one is read only by whoever may look
at the certificates - it names every charging station and every host - so a
monitor signs in with an API key of an account in the `operator` role. Nothing
is added from outside: there is no `add-chain`.

The CRLs, the certificates of the CAs and the OCSP responder are open to
everybody, as RFC 5280 and RFC 6960 have them: whoever checks a certificate does
so before signing in to anything, and what they say names nobody. One request
asks about the certificates of one CA. Asked with GET, the Base64 of the
request goes into the path with its slashes as they are: the HTTP server
refuses a path with `%2F` in it, and a client that writes them so is to POST.


### Who may do what

The node's `viewer` may look at everything and `systemadmin` may do
everything. A PKI adds two resources, `authorities` - making, revoking and
deleting CAs, signing their CRLs by hand and setting the address the PKI is
reached at, and what EST signs - and `issuance` - signing certificates for
servers and clients, also over EST, downloading their keys, also as PKCS#12,
revoking and deleting them - and two roles: `operator`, who looks at
everything and asks whether the network works, and `registrar`, who signs
certificates below the CAs that are there. The `roles` section of
`configuration.json` may add more, as on every node.


### What is not here yet

CMC over EST (`fullcmc`), and a key from `serverkeygen` encrypted to a key of
the station's rather than protected by TLS alone; the certificates of ISO
15118-20, with their keys of secp521r1 and Ed448; and OCPP's `SignCertificate`
taken from a CSMS rather than the CSMS enrolling over EST.


## Your participation

This software is Open Source under the **Affero GPL 3.0 license**.
We appreciate your participation in this ongoing project, and your help to
improve it and the e-mobility ICT in general. If you find bugs, want to
request a feature or send us a pull request, feel free to use the normal
GitHub features to do so. For this please read the Contributor License
Agreement carefully and send us a signed copy or use a similar free and
open license.
