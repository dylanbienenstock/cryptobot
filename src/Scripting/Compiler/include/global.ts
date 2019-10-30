declare const INSTANCE_ID;
declare const INSTANCE_EXCHANGE;
declare const INSTANCE_PAIR;

declare const EXCHANGES: { name: string, pairs: string[] }[];

type TimeFrame = "1m" | "3m" | "5m" | "15m" | "30m" | "1h" | "2h" | "4h" | "6h" | "8h" | "12h" | "1d" | "3d" | "1w" | "1M";
type Aspect = "open" | "high" | "low" | "close";

const Seconds = (n: number) =>  n * 1000;
const Minutes = (n: number) =>  n * 60000;
const Hours   = (n: number) =>  n * 3600000;
const Days    = (n: number) =>  n * 86400000;

enum Signal {
    StrongBuy  = 1,
    Buy        = 0.5,
    Neutral    = 0,
    Sell       = -0.5,
    StrongSell = -1
}

const __createRequireIndicatorDecorator = 
    /// @ts-ignore
    (name: string, settings: Object & { timeFrame: string }): Indicator => {
        let timeFrame = settings.timeFrame;
        delete settings.timeFrame;
        
        /// @ts-ignore
        return __indicators.requireIndicator(name, timeFrame, settings);
    }

function __requireIndicator<T>(name: string, settings: Object & { timeFrame: string }): T {
    let timeFrame = settings.timeFrame;
    delete settings.timeFrame;
    /// @ts-ignore
    return __indicators.requireIndicator(name, timeFrame, settings);
}

/// @ts-ignore
function __requireMultiIndicator<T>(name: string, settings: Object & { timeFrame: string }): IndicatorMultiInstance<T> {
    let timeFrame = settings.timeFrame;
    delete settings.timeFrame;
    /// @ts-ignore
    return __indicators.requireMultiIndicator(name, timeFrame, settings);
}

namespace Input {
    export interface RangeOptions {
        /** Specifies key in Strategy configuration */
        key: string;
        /** Specifies external label */
        label: string;
        /** Specifies integer-only */
        integer: boolean;
        /** Specifies the range of acceptable values */
        range: { min: number, max: number };
        /** Specifies the default value */
        $default: number;
    }

    export function Range(options: RangeOptions): number {
        /// @ts-ignore
        return __input.requireRangeInput(
            options.key,
            options.label,
            options.integer,
            options.range.min,
            options.range.max,
            options.$default
        );
    }

    export interface AspectOptions {
        /** Specifies key in Strategy configuration */
        key: string;
        /** Specifies external label */
        label: string;
        /** Specifies the default value */
        $default: Aspect;
    }

    export function Aspect(options: AspectOptions): Aspect {
        return options.$default;
    }

    export interface TimeFrameOptions {
        /** Specifies key in Strategy configuration */
        key: string;
        /** Specifies external label */
        label: string;
        /** Specifies the default value */
        $default: TimeFrame;
    }

    export function TimeFrame(options: TimeFrameOptions): TimeFrame {
        return options.$default;
    }
}

namespace Interval {
    export function Milliseconds(n: number, immediate?: boolean) {
        return function (target: Object, key: string | symbol) {
            let invoke: Function = target[key];
            if (immediate) invoke();
            /// @ts-ignore
            __interval.createInterval(n, invoke);
        }
    }

    export function Seconds(n: number, immediate?: boolean) {
        return Milliseconds(Seconds(n), immediate);
    }

    export function Minutes(n: number, immediate?: boolean) {
        return Milliseconds(Minutes(n), immediate);
    }

    export function Hours(n: number, immediate?: boolean) {
        return Milliseconds(Hours(n), immediate);
    }

    export function Days(n: number, immediate?: boolean) {
        return Milliseconds(Days(n), immediate);
    }
}