import { Component, Input, ElementRef, ViewChild, Output, EventEmitter } from '@angular/core';
import { DataService } from 'src/app/services/data.service';
import { MenuSection, Menu, MenuOptionClickEvent, MenuOption } from './menu.component.types';

@Component({
    selector: 'app-filter',
    templateUrl: './filter.component.html',
    styleUrls: ['./filter.component.scss']
})
export class FilterComponent {

    constructor(private dataService: DataService) { }

    @ViewChild("container") containerRef: ElementRef;
    private get container(): HTMLElement { return this.containerRef.nativeElement; }

    private _show: boolean = false;
    get show(): boolean { return this._show; }
    @Input() set show(val: boolean) {
        this.ready = false;
        this.menuReady.emit(false);

        this._show = val;
        this.onShowChanged();
    };

    @Input() menu: Menu;
    @Output() menuReady = new EventEmitter<boolean>();
    @Output() optionToggled = new EventEmitter<MenuOptionClickEvent>()

    public ready: boolean = false;
    public rightToLeft: boolean = false;
    public bottomToTop: boolean = false;
    public selectedSection: MenuSection;

    public onSelectSection(section: MenuSection): void {
        if (this.selectedSection && (section.text == this.selectedSection.text))
            return this.selectedSection = null;

        this.selectedSection = section;
    }

    public onShowChanged(): void {
        // Reset the menu
        this.selectedSection = null;

        if (this.show) {
            setTimeout(() => {
                // Determine if it needs to be flipped to be fully onscreen
                let bounds = this.container.getBoundingClientRect();
                this.rightToLeft = bounds.right > window.innerWidth;
                this.bottomToTop = bounds.bottom > window.innerHeight;
                
                setTimeout(() => {
                    this.ready = true;
                    this.menuReady.emit(true);
                });
            });
        } else {
            this.rightToLeft = false;
            this.bottomToTop = false;
        }
    }

    public onToggleOption(option: MenuOption) {
        this.optionToggled.emit({
            sectionText: this.selectedSection.text,
            optionText: option.text,
            setEnabled: (enabled: boolean, text?: string) => {
                option.enabled = enabled;
                option.enabledText = text;
            }
        });
    }
}
