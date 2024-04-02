// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

type PackageReference = {
    PackageId: string
    Version: string
}

type MetadataOverride = {
    SpdxExpression: string
    Copyright: string
}
