export interface Menu {
    sections: MenuSection[];
}

export interface MenuSection {
    text: string;
    options: MenuOption[];
}

export interface MenuOption {
    text: string;
    enabled: boolean;
    enabledText?: string;
}

export interface MenuOptionClickEvent {
    sectionText: string;
    optionText: string;
    setEnabled: (enabled: boolean, text?: string) => void;
}