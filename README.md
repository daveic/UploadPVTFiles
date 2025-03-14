# UploadPVTFiles - BDAPricer

Repository di appoggio per aggiornamento automatico base dati json dell'app BDAPricer, sviluppata da Davide Alessi

La webapp è stata sviluppata con l'obiettivo di realizzare un calcolatore automatico che, presa in input una stringa codice identificativo della tipologia di valvola, ne analizzasse la combinazione dei caratteri e calcolasse il relativo prezzo.
In particolare l'app si appoggia ad un db locale in formato json contenente la mappatura completa dei costi delle singole componenti.
In dipendenza del codice inserito viene recuperato il disegno "tipo" della valvola presa in considerazione.
E' stata implementata una logica di controllo errori per impedire inserimenti errati o fuori contesto.

![Screenshot 2025-03-14 175935](https://github.com/user-attachments/assets/95961a0f-7771-40ba-9621-b53fa93c3152)

Nella sezione Configurazione, accessibile solo da uno specifico utente admin, è possibile andare a modificare i dati presenti sul db.
Viene riportata a schermo la tabella dei dati che saranno plausibilmente modificabili ogni tot mesi, mentre è data la possibilità all'utente di andare a modificare in toto le altre tabelle del db tramite download, modifica e upload del file json stesso.

![Screenshot 2025-03-14 175958](https://github.com/user-attachments/assets/437c1b76-e809-4d5e-a9f9-03d33484f66e)

La webapp risiede su Microsoft Azure, è stata implementata l'autenticazione tramite OAuth con utenze Microsoft definite nel tenant dedicato.

La compilazione e pubblicazione dell'app avviene tramite Action Github definita sul repository privato dedicato al codice sorgente dell'app stessa.
