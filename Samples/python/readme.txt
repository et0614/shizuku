The asyncore module was used for asynchronous processing in BACpypes, 
but this module has been removed in Python version 3.12. 
As a result, a new BACnet communication module, BACpypes3, has been developed using asyncio.

bacpypesでは非同期処理のためにasyncoreが使われていたが、このモジュールはpython version 3.12で削除された。
このため、asyncioを使った新しいbacnet通信モジュールとしてbacpypes3が開発されている。