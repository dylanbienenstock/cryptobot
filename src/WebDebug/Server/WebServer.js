const express = require("express");
const app = express();
const destServer = require("http").Server(app);
const destIO = require("socket.io")(destServer);
const { Socket } = require("net");
const { join } = require("path");

// CLIENT
const tcpClient = new Socket({ 
    readable: true, 
    writable: true,
    allowHalfOpen: true
})
.setNoDelay(true)
.setEncoding("utf-8");

tcpClient.connect(8008, "127.0.0.1", () => {
    console.log("Connected to source server at 127.0.0.1:8008");
});

let accumData = "";

tcpClient.on("data", (chunk) => {
    let accumLength = accumData.length;
    let lastFullMsgEnd = chunk.lastIndexOf("\n");
    
    accumData += chunk;

    if (lastFullMsgEnd == -1) return;

    accumData
        .substr(0, accumLength + lastFullMsgEnd)
        .replace(/\_base/gm, "base")
        .split(/\n/gm)
        .map(msg => JSON.parse(msg))
        .forEach(msg => onReceiveTcpMessage(msg));

    accumData = chunk.substr(lastFullMsgEnd + 1);
});

tcpClient.on("close", () => {
	console.log("Connection to source server closed");
});

tcpClient.on("error", (err) => {
    console.error(err);
});

function onReceiveTcpMessage(msg) {
    console.log("RECEIVED:", msg);

    destIO.emit(msg.type, msg);
}

function sendTcpMessage(msg) {
    if (!tcpClient.write(msg)) {
        tcpClient.once("drain", () => {
            sendTcpMessage(msg);
        });
    } else {
        console.log("SENT:", msg.replace("\n", ""));
    }
}

// DESTINATION
destServer.listen(9009);

console.log("Destination server listening on port 9009");

app.use(express.static(join(__dirname, "../Client/dist/Client")));

app.get("*", (_, res) => {
    res.sendFile(join(__dirname, "../Client/dist/Client/index.html"));
});

destIO.on("connection", (socket) => {
    console.log("Connected to destination");

    socket.on("get", (request) => {
        request.body = JSON.stringify(request.body);
        let msg = JSON.stringify(request) + "\n";

        sendTcpMessage(msg);
    });
});