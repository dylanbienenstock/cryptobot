import { Component } from '@angular/core';
import { DataService } from './services/data.service';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss']
})
export class AppComponent {
    title = 'Client';

    constructor(private dataService: DataService) {
        dataService.listen();

        let asd = () => {
            try {

                console.log(dataService.Network.getBestBidFor({ base: "BTC", quote: "USD" }));   
            } catch {}

            setTimeout(asd, 200);
        }
    }
}
