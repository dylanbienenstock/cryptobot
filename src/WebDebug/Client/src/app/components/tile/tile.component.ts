import { Component, OnInit, Input, EventEmitter, Output, ChangeDetectorRef, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { v1 as uuid } from "uuid"; 
import { DataService } from 'src/app/services/data.service';
import { Menu, MenuSection, MenuOption, MenuOptionClickEvent } from '../filter/menu.component.types';
import { take } from "rxjs/operators";
import { UpdateFilter } from 'src/app/services/data.service.types';
import { Visualization, Side, BindMode, OrderList } from "@dbstock/exchange-network";
import { VisFrame } from '@dbstock/exchange-network/dist/Visualization/VisFrame';
import { TimeSeries } from '@dbstock/exchange-network/dist/TimeSeries';

interface TileBinding {
    exchangeName: string;
    pairStr: string;
    orderList: OrderList;
    timeSeries: TimeSeries;
    side: Side | null;
};

interface TileState {
    uuid?: string;
    splitHorizontal?: boolean;
    splitVertical?: boolean;
    text?: string;
    bindings: TileBinding[];
    children?: TileState[];
}

@Component({
    selector: 'app-tile',
    templateUrl: './tile.component.html',
    styleUrls: ['./tile.component.scss']
})
export class TileComponent implements OnInit, AfterViewInit {

    constructor(private dataService: DataService) { }
    
    @Input() root: boolean;
    @Input() depthVertical: number = 0;
    @Input() depthHorizontal: number = 0;
    @Input() state: TileState = {
        uuid: uuid(),
        splitHorizontal: false,
        splitVertical: false,
        text: "",
        bindings: [],
        children: []
    };

    @Output() propagateMerge = new EventEmitter<string>();

    public veilPresentInDOM: boolean = false;
    public showVeil: boolean = false;
    public showFilterMenu: boolean = false;
    public showViewMenu: boolean = false;
    public filterMenuReady: boolean = false;
    public viewMenuReady: boolean = false;

    public filterMenu: Menu;
    public filter: UpdateFilter;

    @ViewChild("visTarget") visTargetRef: ElementRef;
    public get visTarget(): HTMLCanvasElement { return this.visTargetRef.nativeElement; }
    private visualization: Visualization;

    ngOnInit() {
        setTimeout(() => {
            if (!this.onExchangeNetworkLoaded()) {
                this.dataService.Exchanges
                    .pipe(take(1))
                    .subscribe(() => {
                        this.onExchangeNetworkLoaded();
                    });
            }
        });
    }

    ngAfterViewInit() {
        setTimeout(() => {

        });
    }

    private refreshChildState() {
        this.state.children = [
            { 
                ...this.state,
                uuid: uuid()
            },
            {
                uuid: uuid(),
                splitHorizontal: false,
                splitVertical: false,
                text: "",
                bindings: [],
                children: []
            }
        ];

        delete this.state.text;
    }

    public onSplitVertical() {
        if (this.state.splitHorizontal) return;

        this.refreshChildState();
        this.state.splitVertical = true;
        delete this.state.splitHorizontal;
    }

    public onSplitHorizontal() {
        if (this.state.splitVertical) return;

        this.refreshChildState();
        this.state.splitHorizontal = true;
        delete this.state.splitVertical;
    }

    public onMerge(uuid: string)
    {        
        let merge = (parentState: TileState) => {
            if (!parentState) return;
            if (!parentState.children) return;
            
            let childIndex = parentState.children.findIndex(c => c.uuid == uuid);
            
            if (childIndex == -1) {
                merge(parentState.children[0]);
                merge(parentState.children[1]);
                return;
            }

            let otherChild = parentState.children[Number(!childIndex)];

            Object.assign(parentState, otherChild);
        };

        merge(this.state);
    }

    public onMergePropagated(uuid: string) {
        if (this.root) this.onMerge(uuid);
        else this.propagateMerge.emit(uuid);
    }

    public onToggleFilterMenu() {
        this.showFilterMenu = !this.showFilterMenu;
        this.onToggleAnyMenu();
    }

    public onToggleAnyMenu() {
        let showVeil = this.showFilterMenu || this.showViewMenu;

        if (showVeil) this.veilPresentInDOM = true;
        else setTimeout(() => {
            this.veilPresentInDOM = false;
        }, 300);

        setTimeout(() => {
            this.showVeil = showVeil;
        });
    }

    public onHideAllMenus() {
        this.showFilterMenu = false;
        this.showViewMenu = false;
        this.filterMenuReady = false;
        this.viewMenuReady = false;
        this.onToggleAnyMenu();
    }

    public onExchangeNetworkLoaded(): boolean {
        if (this.dataService.Network.exchanges.size == 0) return false;
        
        this.buildFilterList();
        this.buildVisualization();

        return true;
    }

    public buildVisualization() {
        this.visualization = new Visualization(this.visTarget);

        let resolution = 250;
        let timespan = 10000;

        this.visualization.onRender((vsFr: VisFrame) => {
                if (this.state.bindings.length == 0) return;

                let timeSeriesArr = this.state.bindings.map(b => b.timeSeries);

                vsFr.setResolution(resolution)
                    .setDomain(timespan)
                    .setRangeFrom(timeSeriesArr)
                    .markTimeInterval(1000);

                    this.state.bindings.forEach(binding => {
                        if (!binding.orderList.tail) return;

                        let curPrice = binding.orderList.tail.price;
                        let color = binding.side == Side.Bid ? "green" : "red";

                        vsFr.plotPrice(binding.timeSeries, curPrice, color);
                    });
        });
    }

    public buildFilterList() {
        if (this.filterMenu) return;

        this.filterMenu = {
            sections: Array.from(this.dataService.Network.exchanges)
                .map(exchange => exchange[1])
                .map(exchange => (<MenuSection> {
                    text: exchange.name,
                    options: Array.from(exchange.markets)
                        .map(market => market[1])
                        .sort((a, b) => {
                            if (a.pair.quote > b.pair.quote) return 1;
                            if (a.pair.quote < b.pair.quote) return -1;
                            if (a.pair.base > b.pair.base) return 1;
                            if (a.pair.base < b.pair.base) return -1;
                        })
                        .map(market => (<MenuOption> {
                            text: `${market.pair.base}/${market.pair.quote}`,
                            enabled: false
                        }))
                }))
        };
    }

    public onUpdateSubscriptionChanged(e: MenuOptionClickEvent) {
        let exchangeName = e.sectionText;
        let pairStr = e.optionText;
        let binding = this.getBinding(exchangeName, pairStr);
        let alreadyEnabled = binding.timeSeries != undefined;
        let currentlyEnabled = this.cycleBinding(binding);

        e.setEnabled(currentlyEnabled, binding.side + "s");
        binding.orderList.unbindTimeSeries(binding.timeSeries, BindMode.Best);

        if (currentlyEnabled) {
            binding.timeSeries = new TimeSeries(20000);
            binding.orderList.bindTimeSeries(binding.timeSeries, BindMode.Best);
        } else {
            this.state.bindings = this.state.bindings
                .filter(b => b != binding);
        }
        
        if (alreadyEnabled == currentlyEnabled) return;

        this.dataService.toggleUpdateFilter(
            this.state.uuid,
            exchangeName,
            pairStr,
            currentlyEnabled
        );
    }

    private getBinding(exchangeName: string, pairStr: string): TileBinding {
        let binding = this.state.bindings.find(b => (
            b.exchangeName == exchangeName &&
            b.pairStr == pairStr
        ));

        if (binding) return binding;

        binding = {
            exchangeName,
            pairStr,
            orderList: null,
            timeSeries: null,
            side: null
        };

        this.state.bindings.push(binding);

        return binding;
    }

    private cycleBinding(binding: TileBinding): boolean {
        switch (binding.side) {
            case null:     binding.side = Side.Bid; break;
            case Side.Bid: binding.side = Side.Ask; break;
            case Side.Ask: return false;
        }

        let pairSplit = binding.pairStr.split("/");
        let pair = { base: pairSplit[0], quote: pairSplit[1] };
        
        binding.orderList = this.dataService.Network
            .getMarket(binding.exchangeName, pair)
            .orderBook.getList(binding.side);

        return true;
    }
}
