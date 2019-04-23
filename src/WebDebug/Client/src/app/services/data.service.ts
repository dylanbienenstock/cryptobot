import { Injectable, EventEmitter } from "@angular/core";
import * as io from "socket.io-client";
import { SnapshotMessage, UpdateMessage, ExchangesMessage, UpdateFilter } from './data.service.types';
import { ExchangeNetwork, Side, Pair, Exchange } from "@dbstock/exchange-network";
import { get } from 'selenium-webdriver/http';

@Injectable({
    providedIn: "root"
})
export class DataService {
    private io: SocketIOClient.Socket;

    public Network = new ExchangeNetwork();

    public Exchanges = new EventEmitter<ExchangesMessage>();
    public Snapshots = new EventEmitter<SnapshotMessage>();
    public Updates = new EventEmitter<UpdateMessage>();

    private networkBuilt = false;
    private updateFilter: UpdateFilter = {};

    public listen(): void {
        this.io = io();

        this.io.on("connect", () => this.onConnected());
        this.io.on("exchanges", (msg) => this.onReceiveExchanges(msg));
        this.io.on("snapshot", (msg) => this.onReceiveSnapshot(msg));
        this.io.on("update", (msg) => this.onReceiveUpdate(msg));

        (window as any).get = this.get.bind(this);
    }

    public get(type: string, body?: any) {
        this.io.emit("get", { type, body });
    }

    private onConnected(): void {
        if (!this.networkBuilt)
            this.get("exchanges", { clientName: "Dylan" });
    }

    private onReceiveExchanges(msg: ExchangesMessage): void {
        this.networkBuilt = true;

        msg.exchanges.forEach((e) => {
            // Build the exchange network
            let exchange = this.Network.addExchange(e.exchangeName);
            e.markets.forEach(m => exchange.addMarket(m.pair));

            // Build the update filter
            this.updateFilter[e.exchangeName] = {};

            e.markets.forEach((m) => {
                let pairStr = `${m.pair.base}/${m.pair.quote}`;

                this.updateFilter[e.exchangeName][pairStr] = {
                    subscribed: false,
                    subscribedTiles: []
                };
            });
        });

        this.Exchanges.emit(msg);
    }

    private onReceiveSnapshot(msg: SnapshotMessage): void {
        let now = Date.now();

        let addOrder = (side: Side, data: [number, number]) => {
            this.Network.recordOrder({
                exchangeName: msg.exchangeName,
                side,
                pair: msg.pair,
                price: data[0],
                amount: data[1],
                time: 0
            });
        }

        msg.orderBook.bids.forEach(bid => addOrder(Side.Bid, bid));
        msg.orderBook.asks.forEach(ask => addOrder(Side.Ask, ask));
    }

    private onReceiveUpdate(msg: UpdateMessage) {
        let side = msg.side == "bid" ? Side.Bid : Side.Ask;

        this.Network.recordOrder({
            exchangeName: msg.exchangeName,
            side,
            pair: msg.pair,
            price: msg.price,
            amount: msg.amount,
            time: new Date(msg.time).getTime()
        });
    }
    
    public toggleUpdateFilter(uuid: string, exchangeName: string, pairStr: string, enabled: boolean) {
        let filter = this.updateFilter[exchangeName][pairStr];
        let changed = false;

        let pair: Pair = {
            base: pairStr.substr(0, 3),
            quote: pairStr.substr(4, 3)
        };

        if (enabled) {
            if (!filter.subscribedTiles.includes(uuid)) {
                filter.subscribedTiles.push(uuid);
                changed = true;
            }
        } else {
            if (filter.subscribedTiles.includes(uuid)) {
                let index = filter.subscribedTiles.indexOf(uuid);
                filter.subscribedTiles.splice(index, 1);
                changed = true;

                let orderBook = this.Network.getMarket(exchangeName, pair).orderBook;
                orderBook.bids.clear();
                orderBook.asks.clear();
            }
        }

        if (!changed) return;

        let shouldBeSubscribed = filter.subscribedTiles.length > 0;

        if (shouldBeSubscribed == filter.subscribed) return;

        this.get("filter", {
            exchangeName,
            pair,
            enabled: shouldBeSubscribed
        });

        filter.subscribed = shouldBeSubscribed;
    }
}