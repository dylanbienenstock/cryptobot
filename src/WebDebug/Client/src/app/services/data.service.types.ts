import { Pair, Side } from '@dbstock/exchange-network';

export interface UpdateFilter {
    [exchangeName: string]: {
        [pairStr: string]: {
            subscribed: boolean;
            subscribedTiles: string[];
        }
    }
}

export interface Message {
    type: string;
}

export interface ExchangesMessage extends Message {
    exchanges: {
        exchangeName: string;
        markets: {
            symbol: string;
            pair: Pair;
        }[];
    }[];
}

export interface SnapshotMessage extends Message {
    exchangeName: string;
    symbol: string;
    pair: Pair;
    orderBook: {
        bids: [number, number][],
        asks: [number, number][]
    };
}

export interface UpdateMessage extends Message {
    exchangeName: string;
    symbol: string;
    pair: Pair;
    side: string;
    price: number;
    amount: number;
    time: number;
}