# Stockr

This is a prototype repository to demonstate a statefull resource API that is well known from the Kubernetes API. This prototype aims to replicate some of the features and make use of them in different domains than just container composition. 

The protype domain is logistics. This domain is heavily based on state and handles many resources that are highly connected. Transports, stocks, locations, autonomous systems etc. are all somehow interlinked and traditional warehousing systems that are built on traditional tierd architectures leveraging ER databases strugle to offer the fexibility that would be needed in this domain.

This project aims to demonstate how a few simple resources stored and managed by an API server together with controllers reacting to changes in the API server can implement complex processes with less convoluted code and a cleaner architecture. This allows to reason about a complex domain in simple terms, isolating functionality, transparently handling state changes and offers faul-tolerance and self-healing out-of the box.

## Concepts

### API server

This component is used by all other parts of the system to propagate it's changes to other components through resources. The API server maintaines the state of these resources but never mutates it at it's own accord. This can only be done by another component like a controller or any other API client sending updates in the form of manifests.

### Resources and Manifests

Resources are objects that represent actual domain objects. The API server keeps a record of all resources and also maintains a history of those resources. Clients submit Manifests which are resources with contextual information like the resources kind, group, version and metadata.

Resources can be labeled. Labels are powerful construct that allow filtering for resources with common traits.

### Controllers

Controllers watch changes in the API server either for individual resources or entire groups of resources in order to react to changes on individual resources. Controllers can subscribe to those change streams and will be notified in real time when a resource is updated by another component and can react to this change appropriately.

## Domain

The sample domain chosen is the domain of ligistics or more precisely: warehousing. In this domain knows processes like Goods-Receive, Shipping, Staging, Provisioning or Commissioning and entities like transports, stocks, materials, quantities and locations.

### Entities

#### Quantity

A quantity represent a specific amount in a specific unit of a particular material.

```json
{
    "kind": "quantity",
    "group": "logistics.stockr.io",
    "version": "v1alpha1",
    "metadata": {
        "name": "s1rk95emjf",
        "labels": {...},
        "annotations": {...}
    },
    "spec": {
        "material": "p146723-11344",
        "qty": "12pcs"
    },
    "status": {...}
}
```
This manifest describes a quantity resource: `12` pieces (`pcs`) of the material `p146723-11344`. It has a unique name.

#### Location

A location is a description of a physical location that can then hold quantities.

```json
{
    "kind": "location",
    "group": "logistics.stockr.io",
    "version": "v1alpha1",
    "metadata": {
        "name": "02-005-10"
        "labels": {
            "locations.stockr.io/mobility": "static",
        },
        "annotations": {...}
    },
    "spec": {
        "name": "02-005-10",,
        "parent": "01-005"
        "capacity": [
            {"amount": 10, "footprint": "eur-1"},
            {"amount": 20, "footprint": "eur-6"}
        ]
    },
    "status": {...}
}
```
This sample location has a unique identifier `02-005-10`, it can take either 10 EUR pallets or 20 half-sized EUR pallets. Its mobility is set to `static` through a label which means that this location cannot be moved. Locations can have parent locations for example spaces that are located in a zone.

Another example of a location that could be moved:
```json
{
    "kind": "location",
    "group": "logistics.stockr.io",
    "version": "v1alpha1",
    "metadata": {
        "name": "OSD4QT7DH52T0VQ",
        "labels": {
            "locations.stockr.io/mobility": "free",
        },
        "annotations": {...}
    },
    "spec": {
        "name": "OSD4QT7DH52T0VQ",
        "parent": "02-005-10",
        "footprint": "eur-1"
    },
    "status": {...}
}
```
This location is different from the first one as is is a mobile (`mobility` is `free`) location - a `eur-1` pallet which is situated in the location `02-005-10`.

#### Transport

A transport resource is used to describe a quantity moving from one location to another. Quantities can also be 'sinked' (i.e. disappear) or sourced (i.e. appear) as is common when goods are received or shipped respectively.

```json
{
    "kind": "transport",
    "group": "logistics.stockr.io",
    "version": "v1alpha1",
    "metadata": {
        "name": "11YYSHE74P",
        "labels": {
            "transports.stockr.io/status": "pending",
            "transports.stockr.io/qty-lock-strategy": "onPickUp"
        },
        "annotations": {...}
    },
    "spec": {
        "name": "11YYSHE74P",
        "source": "OSD4QT7DH52T0VQ",
        "target": "sink",
        "material": "p146723-11344",
        "qty": "1pcs"
    },
    "status": {...}
}
```