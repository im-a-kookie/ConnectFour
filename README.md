# ConnectFour

WIP

A backend library for containerized logic models hosted in a multithreaded environment, with an explicitly defined messaging framework for inter-model communication. Designed towards MVVM and Observer Patterns, and intended to be delivered by a desktop application or ASP.NET service.

# Architecture and Scoping

Model Provider defines the model and messaging specifications. Messages are generally Signals, comprising of a string associated header, and optional data packet. Messages are defined in string-delegate pairs, with reflection-based automated generation. Messages can also serialize types via ISerializable and custom translation.

Signals are funnelled through a routing registry into BlockingCollections, and are consumed by the logic model instances. The message pipeline can be observed via events. 

Models can be configured to output results into message queues, results can be observed directly via events, and queries can be awaited using Task API.

Model instances are designed to request a ThreadContainer and subscribe to a loop event. This allows extensibility in the underlying thread framework, and the system is designed to allow the thread framework to be managed in a variety of different configurations (dedicated threads for each model, threadpool, etc). The thread framework should probably be provided via DI pattern.

TODO:

Most of the above.
