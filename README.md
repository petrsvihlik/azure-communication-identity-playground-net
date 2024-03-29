# Azure Communication Services - Identity API Playground
A console app project that allows running various [Azure Communication API endpoints](https://docs.microsoft.com/en-us/azure/communication-services/) via REST or via SDKs.

![image](https://user-images.githubusercontent.com/9810625/148382362-21aebbf5-91be-4e7f-ac8a-b728924d451b.png)

# Sample config

```json
{
  "AAD": {
    "SingleTenant": {
      "ClientID": "<guid>", // Fabrikam's app
      "TenantID": "<guid>" // Fabrikam's tenant
    },
    "MultiTenant": {
      "ClientID": "<guid>", // Contoso's app
      "TenantID": "<guid>", // Fabrikam's tenant
    }
  },
  "PROD": {
    "ResourceName": "<resourcename>",
    "Secret": "YWRmYWQ="
  },
  "PPE": {
    "ResourceName": "<resourcename>",
    "Secret": "YWRmYWQ="
  }
}
```